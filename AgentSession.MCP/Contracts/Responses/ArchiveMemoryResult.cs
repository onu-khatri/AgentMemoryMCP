namespace AgentSession.MCP.Contracts;

public sealed record ArchiveMemoryResult(
    string ArchiveId,
    IReadOnlyList<string> MemoryIds,
    int ArchivedCount
);
