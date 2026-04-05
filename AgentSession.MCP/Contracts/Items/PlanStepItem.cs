using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class PlanStepItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

