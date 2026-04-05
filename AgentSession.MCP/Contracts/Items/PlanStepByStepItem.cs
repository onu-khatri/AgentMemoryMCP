using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class PlanStepByStepItem
{
    [JsonPropertyName("notes")]
    public IReadOnlyCollection<string> Notes { get; set; } = [];

    [JsonPropertyName("steps")]
    public IReadOnlyCollection<PlanStepItem> Steps { get; set; } = [];
}

