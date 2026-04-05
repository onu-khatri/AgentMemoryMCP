using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class ListAgentSessionsItem
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("current_state_summary")]
    public string? CurrentStateSummary { get; set; }

    [JsonPropertyName("artifact_count")]
    public int ArtifactCount { get; set; }

    [JsonPropertyName("last_log_timestamp")]
    public DateTimeOffset? LastLogTimestamp { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

