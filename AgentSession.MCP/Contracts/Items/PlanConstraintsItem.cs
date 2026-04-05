using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class PlanConstraintsItem
{
    [JsonPropertyName("project_knowledge")]
    public IReadOnlyCollection<string> ProjectKnowledge { get; set; } = [];

    [JsonPropertyName("architectural_constraints")]
    public IReadOnlyCollection<string> ArchitecturalConstraints { get; set; } = [];
}

