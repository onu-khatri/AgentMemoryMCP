using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class SaveFinalPlanResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("artifact_name")]
    public string ArtifactName { get; set; } = string.Empty;

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("plan_version")]
    public string? PlanVersion { get; set; }

    [JsonPropertyName("saved_at")]
    public DateTimeOffset SavedAt { get; set; }
}

