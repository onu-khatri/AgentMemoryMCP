using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace AgentSession.MCP.Models;

public sealed class AgentSessionLogEntry
{
    [YamlMember(Alias = "timestamp")]
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [YamlMember(Alias = "message")]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [YamlMember(Alias = "agent_name")]
    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    [YamlMember(Alias = "level")]
    [JsonPropertyName("level")]
    public string Level { get; set; } = "Info";

    [YamlMember(Alias = "event_type")]
    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [YamlMember(Alias = "correlation_id")]
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; set; }
}
