using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class FinalPlanDetailsItem
{
    [JsonPropertyName("plan_version")]
    public string? PlanVersion { get; set; }

    [JsonPropertyName("approved_decisions")]
    public IReadOnlyCollection<string> ApprovedDecisions { get; set; } = [];

    [JsonPropertyName("acceptance_criteria")]
    public IReadOnlyCollection<string> AcceptanceCriteria { get; set; } = [];

    [JsonPropertyName("out_of_scope")]
    public IReadOnlyCollection<string> OutOfScope { get; set; } = [];

    [JsonPropertyName("validation_requirements")]
    public IReadOnlyCollection<string> ValidationRequirements { get; set; } = [];

    [JsonPropertyName("rollback_or_fallback_expectations")]
    public IReadOnlyCollection<string> RollbackOrFallbackExpectations { get; set; } = [];

    [JsonPropertyName("constraints")]
    public PlanConstraintsItem? Constraints { get; set; }

    [JsonPropertyName("assumptions")]
    public IReadOnlyCollection<string> Assumptions { get; set; } = [];

    [JsonPropertyName("impact_analysis")]
    public PlanImpactAnalysisItem? ImpactAnalysis { get; set; }

    [JsonPropertyName("implementation_strategy")]
    public PlanImplementationStrategyItem? ImplementationStrategy { get; set; }

    [JsonPropertyName("step_by_step_plan")]
    public PlanStepByStepItem? StepByStepPlan { get; set; }

    [JsonPropertyName("technical_risks")]
    public IReadOnlyCollection<PlanRiskItem> TechnicalRisks { get; set; } = [];

    [JsonPropertyName("open_questions")]
    public IReadOnlyCollection<string> OpenQuestions { get; set; } = [];

    [JsonPropertyName("required_but_skipped_decisions")]
    public IReadOnlyCollection<string> RequiredButSkippedDecisions { get; set; } = [];

    [JsonPropertyName("approval")]
    public PlanApprovalItem? Approval { get; set; }
}

