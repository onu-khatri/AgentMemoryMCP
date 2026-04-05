using AgentSession.MCP.Models;
using AgentSession.MCP.Tools;

namespace AgentSession.MCP.Services;

internal static class AgentSessionContractMapper
{
    public static ArtifactItem ToArtifactItem(AgentArtifactMetadata metadata)
        => new()
        {
            Name = metadata.Name,
            Description = metadata.Description,
            IntendedUse = metadata.IntendedUse,
            ContentType = metadata.ContentType,
            CreatedAt = metadata.CreatedAt,
            UpdatedAt = metadata.UpdatedAt,
            Tags = metadata.Tags,
            FileName = metadata.FileName
        };

    public static FinalPlanDetails? ToFinalPlanDetails(SaveFinalPlanRequest request)
    {
        var details = new FinalPlanDetails
        {
            PlanVersion = request.PlanVersion?.Trim(),
            ApprovedDecisions = CleanList(request.ApprovedDecisions),
            AcceptanceCriteria = CleanList(request.AcceptanceCriteria),
            OutOfScope = CleanList(request.OutOfScope),
            ValidationRequirements = CleanList(request.ValidationRequirements),
            RollbackOrFallbackExpectations = CleanList(request.RollbackOrFallbackExpectations),
            Constraints = request.Constraints is null ? null : new FinalPlanConstraints
            {
                ProjectKnowledge = CleanList(request.Constraints.ProjectKnowledge),
                ArchitecturalConstraints = CleanList(request.Constraints.ArchitecturalConstraints)
            },
            Assumptions = CleanList(request.Assumptions),
            ImpactAnalysis = request.ImpactAnalysis is null ? null : new FinalPlanImpactAnalysis
            {
                AffectedModuleComponents = CleanList(request.ImpactAnalysis.AffectedModuleComponents),
                CrossCuttingConcerns = CleanList(request.ImpactAnalysis.CrossCuttingConcerns),
                PotentialSideEffects = CleanList(request.ImpactAnalysis.PotentialSideEffects)
            },
            ImplementationStrategy = request.ImplementationStrategy is null ? null : new FinalPlanImplementationStrategy
            {
                Summary = request.ImplementationStrategy.Summary?.Trim(),
                Approach = CleanList(request.ImplementationStrategy.Approach)
            },
            StepByStepPlan = request.StepByStepPlan is null ? null : new FinalPlanStepByStep
            {
                Notes = CleanList(request.StepByStepPlan.Notes),
                Steps = request.StepByStepPlan.Steps
                    .Where(step => !string.IsNullOrWhiteSpace(step.Title))
                    .Select(step => new FinalPlanStep
                    {
                        Title = step.Title.Trim(),
                        Note = step.Note?.Trim()
                    })
                    .ToList()
            },
            TechnicalRisks = request.TechnicalRisks?
                .Where(risk => !string.IsNullOrWhiteSpace(risk.Risk))
                .Select(risk => new FinalPlanRisk
                {
                    Risk = risk.Risk.Trim(),
                    Mitigation = risk.Mitigation?.Trim()
                })
                .ToList() ?? [],
            OpenQuestions = CleanList(request.OpenQuestions),
            RequiredButSkippedDecisions = CleanList(request.RequiredButSkippedDecisions),
            Approval = request.Approval is null
                ? null
                : new FinalPlanApproval
                {
                    IsApproved = request.Approval.IsApproved,
                    ApprovalTimestampUtc = request.Approval.ApprovalTimestamp?.ToUniversalTime().ToString("O")
                }
        };

        var hasData = !string.IsNullOrWhiteSpace(details.PlanVersion)
            || details.ApprovedDecisions.Count > 0
            || details.AcceptanceCriteria.Count > 0
            || details.OutOfScope.Count > 0
            || details.ValidationRequirements.Count > 0
            || details.RollbackOrFallbackExpectations.Count > 0
            || details.Constraints is not null
            || details.Assumptions.Count > 0
            || details.ImpactAnalysis is not null
            || details.ImplementationStrategy is not null
            || details.StepByStepPlan is not null
            || details.TechnicalRisks.Count > 0
            || details.OpenQuestions.Count > 0
            || details.RequiredButSkippedDecisions.Count > 0
            || details.Approval is not null;

        return hasData ? details : null;
    }

    public static FinalPlanDetailsItem? ToFinalPlanDetailsItem(FinalPlanDetails? details)
    {
        if (details is null)
        {
            return null;
        }

        return new FinalPlanDetailsItem
        {
            PlanVersion = details.PlanVersion,
            ApprovedDecisions = details.ApprovedDecisions,
            AcceptanceCriteria = details.AcceptanceCriteria,
            OutOfScope = details.OutOfScope,
            ValidationRequirements = details.ValidationRequirements,
            RollbackOrFallbackExpectations = details.RollbackOrFallbackExpectations,
            Constraints = details.Constraints is null ? null : new PlanConstraintsItem
            {
                ProjectKnowledge = details.Constraints.ProjectKnowledge,
                ArchitecturalConstraints = details.Constraints.ArchitecturalConstraints
            },
            Assumptions = details.Assumptions,
            ImpactAnalysis = details.ImpactAnalysis is null ? null : new PlanImpactAnalysisItem
            {
                AffectedModuleComponents = details.ImpactAnalysis.AffectedModuleComponents,
                CrossCuttingConcerns = details.ImpactAnalysis.CrossCuttingConcerns,
                PotentialSideEffects = details.ImpactAnalysis.PotentialSideEffects
            },
            ImplementationStrategy = details.ImplementationStrategy is null ? null : new PlanImplementationStrategyItem
            {
                Summary = details.ImplementationStrategy.Summary,
                Approach = details.ImplementationStrategy.Approach
            },
            StepByStepPlan = details.StepByStepPlan is null ? null : new PlanStepByStepItem
            {
                Notes = details.StepByStepPlan.Notes,
                Steps = details.StepByStepPlan.Steps.Select(step => new PlanStepItem
                {
                    Title = step.Title,
                    Note = step.Note
                }).ToList()
            },
            TechnicalRisks = details.TechnicalRisks.Select(risk => new PlanRiskItem
            {
                Risk = risk.Risk,
                Mitigation = risk.Mitigation
            }).ToList(),
            OpenQuestions = details.OpenQuestions,
            RequiredButSkippedDecisions = details.RequiredButSkippedDecisions,
            Approval = details.Approval is null ? null : new PlanApprovalItem
            {
                IsApproved = details.Approval.IsApproved,
                ApprovalTimestamp = ParseUtcTimestamp(details.Approval.ApprovalTimestampUtc)
            }
        };
    }

    private static List<string> CleanList(IReadOnlyCollection<string>? values)
        => values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];

    private static DateTimeOffset? ParseUtcTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }
}
