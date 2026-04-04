using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace AgentSession.MCP.Models;

public sealed class ApprovalStatus
{
    [YamlMember(Alias = "is_approved")]
    [JsonPropertyName("is_approved")]
    public bool IsApproved { get; set; }

    [YamlMember(Alias = "approval_timestamp")]
    [JsonPropertyName("approval_timestamp")]
    public DateTimeOffset? ApprovalTimestamp { get; set; }
}
