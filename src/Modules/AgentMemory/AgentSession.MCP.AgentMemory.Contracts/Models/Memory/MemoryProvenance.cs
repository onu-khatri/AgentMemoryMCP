namespace AgentSession.MCP.Models.Memory;

public sealed record MemoryProvenance(
    string MemoryId,
    string RepositoryId,
    long Revision,
    string ContentHash,
    string? SessionId,
    string? TaskId,
    string? AgentId,
    DateTimeOffset CreatedAtUtc
);
