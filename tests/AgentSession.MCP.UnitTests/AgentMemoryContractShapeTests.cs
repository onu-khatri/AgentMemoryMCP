using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using AgentSession.MCP.AgentMemory.Contracts.Capabilities;
using AgentSession.MCP.AgentMemory.Contracts.Registration;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.UnitTests;

public sealed class AgentMemoryContractShapeTests
{
    [Fact]
    public void Agent_memory_capability_surface_is_exactly_twenty_context_aware_methods()
    {
        var methods = new[]
        {
            typeof(IMemoryQueryCapabilities), typeof(IMemoryCommandCapabilities), typeof(IMemoryIndexCapabilities),
        }.SelectMany(type => type.GetMethods()).ToArray();
        Assert.Equal(20, methods.Length);
        Assert.Equal(20, methods.Select(method => method.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(methods, method =>
        {
            var parameters = method.GetParameters();
            Assert.Equal(typeof(PlatformRequestContext), parameters[^2].ParameterType);
            Assert.Equal(typeof(CancellationToken), parameters[^1].ParameterType);
        });
        Assert.Equal(2, methods.Single(method => method.Name == "GetStatusAsync").GetParameters().Length);
        Assert.Equal(2, methods.Single(method => method.Name == "RunMaintenanceAsync").GetParameters().Length);
        Assert.Equal(2, methods.Single(method => method.Name == "RebuildIndexesAsync").GetParameters().Length);
    }

    [Fact]
    public void Agent_memory_defaults_bounds_and_enums_are_preserved()
    {
        var remember = new RememberMemoryRequest { OperationId = "op", Tier = RememberTier.Temp, Content = "content" };
        Assert.Equal(string.Empty, remember.Title);
        Assert.Equal("observation", remember.Category);
        Assert.Equal("agent", remember.SourceType);
        Assert.Empty(remember.Tags);
        Assert.False(remember.Protected);
        var recall = new RecallMemoryRequest();
        Assert.Equal(10, recall.MaxResults);
        Assert.Equal([MemoryTier.Temp, MemoryTier.Short, MemoryTier.Long], recall.Tiers);
        Assert.False(new MemoryWriteResult("id", 1, MemoryTier.Temp, MemoryState.Active).Duplicate);
        Assert.Equal(0, new CleanupMemoryResult(0, 0).Archived);
        Assert.True(new MemoryReadResult(null, "not_found").Advisory);
        Assert.Equal(100, typeof(ReindexMemoryRequest).GetProperty(nameof(ReindexMemoryRequest.MaxItems))!
            .GetCustomAttribute<RangeAttribute>()!.Maximum);
    }

    [Fact]
    public void Agent_memory_json_preserves_snake_case_enums_and_rejects_integer_values()
    {
        var json = JsonSerializer.Serialize(ReviewState.CompactCandidate, ContractJson.Options);
        Assert.Equal("\"compact_candidate\"", json);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MemoryTier>("2", ContractJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RecallMemoryRequest>("{\"unknown\":true}", ContractJson.Options));
    }

    [Fact]
    public void Embedding_metadata_is_provider_neutral_and_nested_shape_is_available()
    {
        var metadata = new EmbeddingMetadata("provider", "model", 3, "v1", null);
        Assert.Null(metadata.ModelDigest);
        Assert.DoesNotContain(typeof(EmbeddingMetadata).GetProperties(), property =>
            property.PropertyType.Namespace?.StartsWith("Qdrant", StringComparison.Ordinal) == true ||
            property.PropertyType.Namespace?.StartsWith("Ollama", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Agent_memory_descriptor_has_exact_operation_inventory()
    {
        Assert.Equal(20, AgentMemoryModuleRegistrationDescriptor.Value.Capabilities.Count);
        Assert.Equal("memory_status", AgentMemoryModuleRegistrationDescriptor.Value.Capabilities[0].OperationId);
        Assert.Equal("memory_rebuild_indexes", AgentMemoryModuleRegistrationDescriptor.Value.Capabilities[^1].OperationId);
    }
}
