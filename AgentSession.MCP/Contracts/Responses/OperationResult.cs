using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class OperationResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

