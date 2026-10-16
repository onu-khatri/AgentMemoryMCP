namespace AgentSession.MCP.Models.Memory;

public sealed record EmbeddingMetadata(
    string Provider,
    string Model,
    int Dimension,
    string EmbeddingVersion,
    DateTimeOffset? IndexedAtUtc,
    string? ModelDigest = null
);
