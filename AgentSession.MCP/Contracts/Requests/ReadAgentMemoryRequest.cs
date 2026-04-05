using System.ComponentModel;

namespace AgentSession.MCP.Tools;

public sealed class ReadAgentMemoryRequest
{
    [Description("Session id to read memory from.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("How many recent log entries to include in summary.")]
    public int LatestLogEntries { get; set; } = 10;
}

