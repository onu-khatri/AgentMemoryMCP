using System.Text.Json;
using AgentSession.MCP.Models.Memory;
using AgentSession.MCP.Services;

namespace AgentSession.MCP.Tests;

public sealed class EmbeddingProjectionTests
{
    [Fact]
    public void ProjectionNormalizesEquivalentContentAndMetadata()
    {
        var first = Record();
        first.Content = "caf\u00e9\r\nclaim";
        first.Tags = ["two", "one"];
        first.StructuredData = JsonSerializer.SerializeToElement(new { b = 2, a = 1 });
        var second = Record();
        second.Content = "cafe\u0301\nclaim";
        second.Tags = ["one", "two"];
        second.StructuredData = JsonSerializer.SerializeToElement(new { a = 1, b = 2 });

        Assert.Equal(EmbeddingProjection.Project(first), EmbeddingProjection.Project(second));
        Assert.StartsWith("projection=projection-v1\n", EmbeddingProjection.Project(first));
    }

    [Fact]
    public void FingerprintAndCollectionNamesSeparateModelsProjectionAndRepositories()
    {
        var first = EmbeddingProjection.Fingerprint("ollama", "model-a", null, 768);
        var modelChange = EmbeddingProjection.Fingerprint("ollama", "model-b", null, 768);
        var projectionChange = EmbeddingProjection.Fingerprint(
            "ollama",
            "model-a",
            null,
            768,
            "projection-v2"
        );
        Assert.NotEqual(first.Hash, modelChange.Hash);
        Assert.NotEqual(first.Hash, projectionChange.Hash);
        var repoOne = EmbeddingProjection.Collection("repo_ai_learning_v1", "repo-one", first);
        var repoTwo = EmbeddingProjection.Collection("repo_ai_learning_v1", "repo-two", first);
        Assert.NotEqual(repoOne.Alias, repoTwo.Alias);
        Assert.NotEqual(repoOne.PhysicalCollection, repoTwo.PhysicalCollection);
        Assert.Contains(first.Hash[..16], repoOne.PhysicalCollection);
    }

    private static MemoryRecord Record() =>
        new()
        {
            Id = "memory",
            RepositoryId = "repo",
            Tier = MemoryTier.Long,
            Content = "claim",
            Category = "observation",
            ContentHash = "hash",
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            OperationId = "operation",
            Status = MemoryState.Candidate,
            EmbeddingState = EmbeddingState.PendingEmbedding,
            IndexState = VectorIndexState.Pending,
        };
}
