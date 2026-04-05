using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class PlanImpactAnalysisItem
{
    [JsonPropertyName("affected_module_components")]
    public IReadOnlyCollection<string> AffectedModuleComponents { get; set; } = [];

    [JsonPropertyName("cross_cutting_concerns")]
    public IReadOnlyCollection<string> CrossCuttingConcerns { get; set; } = [];

    [JsonPropertyName("potential_side_effects")]
    public IReadOnlyCollection<string> PotentialSideEffects { get; set; } = [];
}

