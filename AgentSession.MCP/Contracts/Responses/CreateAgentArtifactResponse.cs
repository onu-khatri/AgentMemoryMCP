using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class CreateAgentArtifactResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public ArtifactItem Metadata { get; set; } = new();
}

