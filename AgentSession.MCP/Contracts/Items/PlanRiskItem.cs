using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class PlanRiskItem
{
    [JsonPropertyName("risk")]
    public string Risk { get; set; } = string.Empty;

    [JsonPropertyName("mitigation")]
    public string? Mitigation { get; set; }
}

