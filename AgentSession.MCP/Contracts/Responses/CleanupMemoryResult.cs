namespace AgentSession.MCP.Contracts;

public sealed record CleanupMemoryResult(int Deleted, int Errors, int Archived = 0);

public sealed record ReviewMemoryCandidate(
    AgentSession.MCP.Models.Memory.MemoryRecord Record,
    AgentSession.MCP.Models.Memory.ReviewState SuggestedAction,
    string Reason,
    bool ExplicitlyApproved
);
