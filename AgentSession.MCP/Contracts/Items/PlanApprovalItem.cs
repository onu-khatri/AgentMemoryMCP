using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class PlanApprovalItem
{
    [JsonPropertyName("is_approved")]
    public bool IsApproved { get; set; }

    [JsonPropertyName("approval_timestamp")]
    public DateTimeOffset? ApprovalTimestamp { get; set; }
}

