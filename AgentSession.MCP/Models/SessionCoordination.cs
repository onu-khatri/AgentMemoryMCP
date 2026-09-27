namespace AgentSession.MCP.Models;

public enum SessionArtifactKind
{
    Context,
    Decision,
    Finding,
    Handoff,
    Plan,
    Output,
}

public enum WorkStatus
{
    Available,
    InProgress,
    Blocked,
    Completed,
}

public enum TaskAction
{
    Create,
    Claim,
    Renew,
    Release,
    Block,
    Complete,
    Reopen,
}

public sealed record SessionChange(
    long Sequence,
    string ActorId,
    string Kind,
    string RecordId,
    long Revision,
    DateTimeOffset TimestampUtc,
    string? Reason = null
);

public sealed record AgentCheckpoint(
    string AgentId,
    long AcknowledgedSequence,
    DateTimeOffset UpdatedAtUtc
);
