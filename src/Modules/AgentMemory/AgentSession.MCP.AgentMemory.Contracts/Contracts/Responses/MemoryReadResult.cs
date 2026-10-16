using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Contracts;

public sealed record MemoryReadResult(
    MemoryRecord? Record,
    string Availability,
    bool Advisory = true
);
