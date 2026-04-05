using System.ComponentModel;

namespace AgentSession.MCP.Tools;

public sealed class GetLatestFinalPlanRequest
{
    [Description("Session id to fetch the latest saved final plan from.")]
    public string SessionId { get; set; } = string.Empty;
}

