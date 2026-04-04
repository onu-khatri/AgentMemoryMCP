using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace AgentSession.MCP.Models;

public sealed class AgentSessionState
{
    public const string AgentMemoryFileName = "agent_memory.md";
    public const string AgentSessionStateFileName = "agent_session_state.yaml";
    public const string AgentSessionLogFileName = "agent_session_log.yaml";

    [YamlMember(Alias = "session_id")]
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [YamlMember(Alias = "created_at")]
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [YamlMember(Alias = "updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [YamlMember(Alias = "agent_name")]
    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = "unspecified";

    [YamlMember(Alias = "current_state")]
    [JsonPropertyName("current_state")]
    public string? CurrentState { get; set; }

    [YamlMember(Alias = "description")]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "tags")]
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [YamlMember(Alias = "version")]
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [YamlMember(Alias = "usage_instruction")]
    [JsonPropertyName("usage_instruction")]
    public string UsageInstruction { get; set; } =
        "You have long-term memory and a logger. Read memory at startup and before critical decisions. Write validated data, durable decisions, reusable context, and important artifacts. Log actions, decisions, errors, and external effects consistently for traceability.";
}
