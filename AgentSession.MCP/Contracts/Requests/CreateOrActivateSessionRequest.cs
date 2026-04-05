using System.ComponentModel;

namespace AgentSession.MCP.Tools;

public sealed class CreateOrActivateSessionRequest
{
    [Description("Optional deterministic session id. If omitted, a human-readable unique id is generated.")]
    public string? SessionId { get; set; }

    [Description("Optional initial state summary for this session.")]
    public string? InitialState { get; set; }

    [Description("Optional agent name activating this session.")]
    public string? AgentName { get; set; }
}

