using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Contracts;

public sealed record MemoryWriteResult(
    string MemoryId,
    long Revision,
    MemoryTier Tier,
    MemoryState Status,
    bool Duplicate = false
);
