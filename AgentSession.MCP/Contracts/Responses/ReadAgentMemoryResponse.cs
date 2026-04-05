using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class ReadAgentMemoryResponse
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("memory")]
    public string Memory { get; set; } = string.Empty;

    [JsonPropertyName("artifacts")]
    public IReadOnlyCollection<ArtifactItem> Artifacts { get; set; } = [];

    [JsonPropertyName("latest_logs")]
    public IReadOnlyCollection<LogItem> LatestLogs { get; set; } = [];

    [JsonPropertyName("metadata")]
    public SessionMetadataItem Metadata { get; set; } = new();
}

