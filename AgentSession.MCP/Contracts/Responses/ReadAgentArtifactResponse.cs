using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class ReadAgentArtifactResponse
{
    [JsonPropertyName("metadata")]
    public ArtifactItem Metadata { get; set; } = new();

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

