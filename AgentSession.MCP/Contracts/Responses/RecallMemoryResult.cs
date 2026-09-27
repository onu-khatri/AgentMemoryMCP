namespace AgentSession.MCP.Contracts;

public sealed record RecallMemoryResult(
    IReadOnlyList<RecalledMemory> Memories,
    bool HasMore,
    string Mode,
    IReadOnlyList<string> HealthReasons
);
