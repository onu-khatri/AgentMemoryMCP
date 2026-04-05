using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class PlanImplementationStrategyItem
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("approach")]
    public IReadOnlyCollection<string> Approach { get; set; } = [];
}

