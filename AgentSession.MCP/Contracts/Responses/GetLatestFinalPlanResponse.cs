using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class GetLatestFinalPlanResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("not_found")]
    public bool NotFound { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public ArtifactItem? Metadata { get; set; }

    [JsonPropertyName("plan_content")]
    public string? PlanContent { get; set; }

    [JsonPropertyName("plan_details")]
    public FinalPlanDetailsItem? PlanDetails { get; set; }
}

