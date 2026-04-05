using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class ListAgentArtifactsResponse
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("artifacts")]
    public IReadOnlyCollection<ArtifactItem> Artifacts { get; set; } = [];
}

