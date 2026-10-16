namespace AgentSession.MCP.Models.Memory;

public sealed class MemoryOutcome
{
    public int SchemaVersion { get; set; } = 1;
    public required string OutcomeId { get; set; }
    public required string MemoryId { get; set; }
    public required string RepositoryId { get; set; }
    public required string SessionId { get; set; }
    public required string TaskId { get; set; }
    public required string AgentId { get; set; }
    public required OutcomeKind Result { get; set; }
    public required List<string> Evidence { get; set; }
    public string? Notes { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed record MemoryConfirmation(
    string OutcomeId,
    string SessionId,
    string AgentId,
    List<string> Evidence
);
