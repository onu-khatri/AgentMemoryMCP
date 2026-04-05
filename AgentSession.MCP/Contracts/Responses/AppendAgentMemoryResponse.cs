using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class AppendAgentMemoryResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("memory_size_bytes")]
    public long MemorySizeBytes { get; set; }
}

