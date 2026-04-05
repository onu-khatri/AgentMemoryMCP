using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class FinalPlanDetailsItem
{
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

