using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class LogItem
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;

    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; set; }
}

