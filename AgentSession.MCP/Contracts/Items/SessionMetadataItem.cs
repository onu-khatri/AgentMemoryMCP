using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class SessionMetadataItem
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("current_state")]
    public string? CurrentState { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

