namespace AgentSession.MCP.Models.Memory;

public sealed class VectorPendingWork
{
    public int SchemaVersion { get; set; } = 1;
    public required string WorkId { get; set; }
    public required string RepositoryId { get; set; }
    public required string MemoryId { get; set; }
    public required long Revision { get; set; }
    public required string ContentHash { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; set; }
    public required DateTimeOffset NextAttemptAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastErrorCode { get; set; }
}

public sealed record VectorWorkResult(
    int Processed,
    int Indexed,
    int Deferred,
    int Errors
);

public sealed record VectorReconciliationResult(
    int MissingOrStaleQueued,
    int OrphansDeleted,
    int Errors
);
