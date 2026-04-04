using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace AgentSession.MCP.Models;

public sealed class FinalPlanDetails
{
    [YamlMember(Alias = "constraints")]
    [JsonPropertyName("constraints")]
    public FinalPlanConstraints? Constraints { get; set; }

    [YamlMember(Alias = "assumptions")]
    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [YamlMember(Alias = "impact_analysis")]
    [JsonPropertyName("impact_analysis")]
    public FinalPlanImpactAnalysis? ImpactAnalysis { get; set; }

    [YamlMember(Alias = "implementation_strategy")]
    [JsonPropertyName("implementation_strategy")]
    public FinalPlanImplementationStrategy? ImplementationStrategy { get; set; }

    [YamlMember(Alias = "step_by_step_plan")]
    [JsonPropertyName("step_by_step_plan")]
    public FinalPlanStepByStep? StepByStepPlan { get; set; }

    [YamlMember(Alias = "technical_risks")]
    [JsonPropertyName("technical_risks")]
    public List<FinalPlanRisk> TechnicalRisks { get; set; } = [];

    [YamlMember(Alias = "open_questions")]
    [JsonPropertyName("open_questions")]
    public List<string> OpenQuestions { get; set; } = [];

    [YamlMember(Alias = "required_but_skipped_decisions")]
    [JsonPropertyName("required_but_skipped_decisions")]
    public List<string> RequiredButSkippedDecisions { get; set; } = [];

    [YamlMember(Alias = "approval")]
    [JsonPropertyName("approval")]
    public FinalPlanApproval? Approval { get; set; }
}

public sealed class FinalPlanApproval
{
    [YamlMember(Alias = "is_approved")]
    [JsonPropertyName("is_approved")]
    public bool IsApproved { get; set; }

    [YamlMember(Alias = "approval_timestamp_utc")]
    [JsonPropertyName("approval_timestamp_utc")]
    public string? ApprovalTimestampUtc { get; set; }
}

public sealed class FinalPlanConstraints
{
    [YamlMember(Alias = "project_knowledge")]
    [JsonPropertyName("project_knowledge")]
    public List<string> ProjectKnowledge { get; set; } = [];

    [YamlMember(Alias = "architectural_constraints")]
    [JsonPropertyName("architectural_constraints")]
    public List<string> ArchitecturalConstraints { get; set; } = [];
}

public sealed class FinalPlanImpactAnalysis
{
    [YamlMember(Alias = "affected_module_components")]
    [JsonPropertyName("affected_module_components")]
    public List<string> AffectedModuleComponents { get; set; } = [];

    [YamlMember(Alias = "cross_cutting_concerns")]
    [JsonPropertyName("cross_cutting_concerns")]
    public List<string> CrossCuttingConcerns { get; set; } = [];

    [YamlMember(Alias = "potential_side_effects")]
    [JsonPropertyName("potential_side_effects")]
    public List<string> PotentialSideEffects { get; set; } = [];
}

public sealed class FinalPlanImplementationStrategy
{
    [YamlMember(Alias = "summary")]
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [YamlMember(Alias = "approach")]
    [JsonPropertyName("approach")]
    public List<string> Approach { get; set; } = [];
}

public sealed class FinalPlanStepByStep
{
    [YamlMember(Alias = "notes")]
    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; } = [];

    [YamlMember(Alias = "steps")]
    [JsonPropertyName("steps")]
    public List<FinalPlanStep> Steps { get; set; } = [];
}

public sealed class FinalPlanStep
{
    [YamlMember(Alias = "title")]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [YamlMember(Alias = "note")]
    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

public sealed class FinalPlanRisk
{
    [YamlMember(Alias = "risk")]
    [JsonPropertyName("risk")]
    public string Risk { get; set; } = string.Empty;

    [YamlMember(Alias = "mitigation")]
    [JsonPropertyName("mitigation")]
    public string? Mitigation { get; set; }
}
