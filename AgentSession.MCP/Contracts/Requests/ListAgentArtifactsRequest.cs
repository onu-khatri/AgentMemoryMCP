using System.ComponentModel;

namespace AgentSession.MCP.Tools;

public sealed class ListAgentArtifactsRequest
{
    [Description("Session id containing artifacts.")]
    public string SessionId { get; set; } = string.Empty;
}

