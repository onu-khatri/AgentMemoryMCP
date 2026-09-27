using System.Text.Json;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;
using AgentSession.MCP.Services;

namespace AgentSession.MCP.Tests;

public sealed class MemoryRecordTests
{
    private static MemoryRecord Temp() => new()
    {
        Id = "record-one", RepositoryId = "repo", Tier = MemoryTier.Temp, Content = "Verified observation",
        ContentHash = "fixture-hash", OperationId = "operation-one", Status = MemoryState.Active,
        CreatedAtUtc = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero),
        UpdatedAtUtc = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero),
        ExpiresAtUtc = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero)
    };

    [Fact]
    public void CanonicalRoundTripPreservesStructuredEvidenceAndExpiry()
    {
        var record = Temp();
        record.StructuredData = JsonSerializer.SerializeToElement(new { file = "service.cs", passed = true });
        record.Relationships.Add(new MemoryRelationship(RelationshipKind.DerivedFrom, "source-record"));
        var json = JsonSerializer.Serialize(record, MemoryJson.Options);
        var restored = JsonSerializer.Deserialize<MemoryRecord>(json, MemoryJson.Options)!;
        MemoryRecordValidator.Validate(restored);
        Assert.Equal(record.ExpiresAtUtc, restored.ExpiresAtUtc);
        Assert.True(restored.StructuredData!.Value.GetProperty("passed").GetBoolean());
        Assert.Equal(RelationshipKind.DerivedFrom, restored.Relationships.Single().Kind);
        Assert.Contains("derived_from", json);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("expiry")]
    [InlineData("embedding")]
    [InlineData("confidence")]
    [InlineData("revision")]
    [InlineData("lifecycle")]
    public void RejectsInvalidCanonicalRecords(string invalid)
    {
        var record = Temp();
        switch (invalid)
        {
            case "schema": record.SchemaVersion = 99; break;
            case "expiry": record.ExpiresAtUtc = record.CreatedAtUtc.AddHours(25); break;
            case "embedding": record.EmbeddingState = EmbeddingState.PendingEmbedding; break;
            case "confidence": record.Confidence = double.NaN; break;
            case "revision": record.Revision = 0; break;
            case "lifecycle": record.Status = MemoryState.Validated; break;
        }
        Assert.Throws<ValidationException>(() => MemoryRecordValidator.Validate(record));
    }

    [Fact]
    public void ValidatedLifecycleSurvivesPendingIndexRoundTrip()
    {
        var record = Temp();
        record.Tier = MemoryTier.Long;
        record.Status = MemoryState.Validated;
        record.ExpiresAtUtc = null;
        record.EmbeddingState = EmbeddingState.PendingEmbedding;
        record.IndexState = VectorIndexState.Pending;
        var restored = JsonSerializer.Deserialize<MemoryRecord>(JsonSerializer.Serialize(record, MemoryJson.Options), MemoryJson.Options)!;
        MemoryRecordValidator.Validate(restored);
        Assert.Equal(MemoryState.Validated, restored.Status);
        Assert.Equal(EmbeddingState.PendingEmbedding, restored.EmbeddingState);
    }

    [Fact]
    public void UnknownPropertiesAndNumericEnumsFailDeserialization()
    {
        var json = JsonSerializer.Serialize(Temp(), MemoryJson.Options);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MemoryRecord>(json.Replace("\"temp\"", "123"), MemoryJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MemoryRecord>(json.Replace("\"content\":", "\"unknown\":"), MemoryJson.Options));
    }

    [Fact]
    public void ExportedSchemasContainRequiredFieldsAndNamedStates()
    {
        var output = Path.Combine(AppContext.BaseDirectory, "schemas");
        Directory.CreateDirectory(output);
        var record = MemoryJson.SchemaFor<MemoryRecord>();
        Assert.Contains("repositoryId", record["required"]!.ToJsonString());
        Assert.Contains("pending_embedding", record.ToJsonString());
        Assert.False(record["additionalProperties"]!.GetValue<bool>());
        File.WriteAllText(Path.Combine(output, "memory-record-v1.schema.json"), record.ToJsonString(MemoryJson.Options));
        File.WriteAllText(Path.Combine(output, "memory-outcome-v1.schema.json"), MemoryJson.SchemaFor<MemoryOutcome>().ToJsonString(MemoryJson.Options));
        File.WriteAllText(Path.Combine(output, "memory-event-v1.schema.json"), MemoryJson.SchemaFor<MemoryEvent>().ToJsonString(MemoryJson.Options));
        File.WriteAllText(Path.Combine(output, "session-artifact-v1.schema.json"), MemoryJson.SchemaFor<AgentSession.MCP.Models.SharedSessionArtifact>().ToJsonString(MemoryJson.Options));
        File.WriteAllText(Path.Combine(output, "session-append-v1.schema.json"), MemoryJson.SchemaFor<AgentSession.MCP.Contracts.UpdateSessionArtifactsRequest>().ToJsonString(MemoryJson.Options));
        File.WriteAllText(Path.Combine(output, "session-task-request-v1.schema.json"), MemoryJson.SchemaFor<AgentSession.MCP.Contracts.CoordinateTaskRequest>().ToJsonString(MemoryJson.Options));
    }
}
