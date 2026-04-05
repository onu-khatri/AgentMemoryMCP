using System.ComponentModel;

namespace AgentSession.MCP.Tools;

public sealed class SaveFinalPlanRequest
{
    [Description("Session id containing the final plan.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("Final implementation plan markdown content.")]
    public string PlanContent { get; set; } = string.Empty;

    [Description("Optional human-readable final plan title.")]
    public string? PlanTitle { get; set; }

    [Description("Optional agent name authoring the final plan.")]
    public string? AgentName { get; set; }

    [Description("Optional approved plan version/id used by implementation executor handoff.")]
    public string? PlanVersion { get; set; }

    [Description("Optional approved decisions from planning that implementation must follow.")]
    public IReadOnlyCollection<string>? ApprovedDecisions { get; set; }

    [Description("Optional acceptance criteria that define done for implementation.")]
    public IReadOnlyCollection<string>? AcceptanceCriteria { get; set; }

    [Description("Optional explicit out-of-scope boundaries for implementation.")]
    public IReadOnlyCollection<string>? OutOfScope { get; set; }

    [Description("Optional required validation checks (build/test/lint/quality gates).")]
    public IReadOnlyCollection<string>? ValidationRequirements { get; set; }

    [Description("Optional rollback or fallback expectations if implementation risk materializes.")]
    public IReadOnlyCollection<string>? RollbackOrFallbackExpectations { get; set; }

    [Description("Optional project knowledge and architectural constraints used in planning.")]
    public PlanConstraintsItem? Constraints { get; set; }

    [Description("Optional assumptions used in planning.")]
    public IReadOnlyCollection<string>? Assumptions { get; set; }

    [Description("Optional impact analysis for this plan.")]
    public PlanImpactAnalysisItem? ImpactAnalysis { get; set; }

    [Description("Optional implementation strategy details.")]
    public PlanImplementationStrategyItem? ImplementationStrategy { get; set; }

    [Description("Optional step-by-step execution notes and steps.")]
    public PlanStepByStepItem? StepByStepPlan { get; set; }

    [Description("Optional technical risks and mitigations.")]
    public IReadOnlyCollection<PlanRiskItem>? TechnicalRisks { get; set; }

    [Description("Optional open technical or business questions.")]
    public IReadOnlyCollection<string>? OpenQuestions { get; set; }

    [Description("Optional decisions that are required but intentionally skipped for now.")]
    public IReadOnlyCollection<string>? RequiredButSkippedDecisions { get; set; }

    [Description("Optional approval status and approval timestamp for this plan.")]
    public PlanApprovalItem? Approval { get; set; }
}

