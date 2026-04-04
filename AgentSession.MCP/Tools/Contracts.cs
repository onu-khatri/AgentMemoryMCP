using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class ListAgentSessionsItem
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("current_state_summary")]
    public string? CurrentStateSummary { get; set; }

    [JsonPropertyName("artifact_count")]
    public int ArtifactCount { get; set; }

    [JsonPropertyName("last_log_timestamp")]
    public DateTimeOffset? LastLogTimestamp { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class CreateOrActivateSessionRequest
{
    [Description("Optional deterministic session id. If omitted, a human-readable unique id is generated.")]
    public string? SessionId { get; set; }

    [Description("Optional initial state summary for this session.")]
    public string? InitialState { get; set; }

    [Description("Optional agent name activating this session.")]
    public string? AgentName { get; set; }
}

public sealed class ReadAgentMemoryRequest
{
    [Description("Session id to read memory from.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("How many recent log entries to include in summary.")]
    public int LatestLogEntries { get; set; } = 10;
}

public sealed class AppendAgentMemoryRequest
{
    [Description("Session id to append memory into.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("Durable memory content to append.")]
    public string Content { get; set; } = string.Empty;

    [Description("Optional agent name authoring this memory.")]
    public string? AgentName { get; set; }

    [Description("Optional markdown section title.")]
    public string? SectionTitle { get; set; }
}

public sealed class LogAgentEventRequest
{
    [Description("Session id to write the event to.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("Log message content.")]
    public string Message { get; set; } = string.Empty;

    [Description("Agent name creating the event.")]
    public string AgentName { get; set; } = string.Empty;

    [Description("Severity level: Info, Warning, Error, Debug.")]
    public string Level { get; set; } = "Info";

    [Description("Optional event category.")]
    public string? EventType { get; set; }

    [Description("Optional correlation id.")]
    public string? CorrelationId { get; set; }
}

public sealed class CreateAgentArtifactRequest
{
    [Description("Session id containing the artifact.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("Artifact name (filesystem-safe).")]
    public string ArtifactName { get; set; } = string.Empty;

    [Description("Human-readable artifact description.")]
    public string Description { get; set; } = string.Empty;

    [Description("How this artifact should be used.")]
    public string IntendedUse { get; set; } = string.Empty;

    [Description("Artifact body content.")]
    public string Content { get; set; } = string.Empty;

    [Description("Optional content type (defaults to text/markdown).")]
    public string? ContentType { get; set; }

    [Description("Optional tags.")]
    public IReadOnlyCollection<string>? Tags { get; set; }

    [Description("Allow overwrite if artifact already exists.")]
    public bool Overwrite { get; set; }
}

public sealed class ReadAgentArtifactRequest
{
    [Description("Session id containing the artifact.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("Artifact name to read.")]
    public string ArtifactName { get; set; } = string.Empty;
}

public sealed class ListAgentArtifactsRequest
{
    [Description("Session id containing artifacts.")]
    public string SessionId { get; set; } = string.Empty;
}

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

public sealed class GetLatestFinalPlanRequest
{
    [Description("Session id to fetch the latest saved final plan from.")]
    public string SessionId { get; set; } = string.Empty;
}

public sealed class ReadAgentMemoryResponse
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("memory")]
    public string Memory { get; set; } = string.Empty;

    [JsonPropertyName("artifacts")]
    public IReadOnlyCollection<ArtifactItem> Artifacts { get; set; } = [];

    [JsonPropertyName("latest_logs")]
    public IReadOnlyCollection<LogItem> LatestLogs { get; set; } = [];

    [JsonPropertyName("metadata")]
    public SessionMetadataItem Metadata { get; set; } = new();
}

public sealed class AppendAgentMemoryResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("memory_size_bytes")]
    public long MemorySizeBytes { get; set; }
}

public sealed class OperationResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class CreateAgentArtifactResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public ArtifactItem Metadata { get; set; } = new();
}

public sealed class ReadAgentArtifactResponse
{
    [JsonPropertyName("metadata")]
    public ArtifactItem Metadata { get; set; } = new();

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class ListAgentArtifactsResponse
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("artifacts")]
    public IReadOnlyCollection<ArtifactItem> Artifacts { get; set; } = [];
}

public sealed class SaveFinalPlanResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("artifact_name")]
    public string ArtifactName { get; set; } = string.Empty;

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("saved_at")]
    public DateTimeOffset SavedAt { get; set; }
}

public sealed class GetLatestFinalPlanResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("not_found")]
    public bool NotFound { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public ArtifactItem? Metadata { get; set; }

    [JsonPropertyName("plan_content")]
    public string? PlanContent { get; set; }

    [JsonPropertyName("plan_details")]
    public FinalPlanDetailsItem? PlanDetails { get; set; }
}

public sealed class PlanConstraintsItem
{
    [JsonPropertyName("project_knowledge")]
    public IReadOnlyCollection<string> ProjectKnowledge { get; set; } = [];

    [JsonPropertyName("architectural_constraints")]
    public IReadOnlyCollection<string> ArchitecturalConstraints { get; set; } = [];
}

public sealed class PlanImpactAnalysisItem
{
    [JsonPropertyName("affected_module_components")]
    public IReadOnlyCollection<string> AffectedModuleComponents { get; set; } = [];

    [JsonPropertyName("cross_cutting_concerns")]
    public IReadOnlyCollection<string> CrossCuttingConcerns { get; set; } = [];

    [JsonPropertyName("potential_side_effects")]
    public IReadOnlyCollection<string> PotentialSideEffects { get; set; } = [];
}

public sealed class PlanImplementationStrategyItem
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("approach")]
    public IReadOnlyCollection<string> Approach { get; set; } = [];
}

public sealed class PlanStepItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

public sealed class PlanStepByStepItem
{
    [JsonPropertyName("notes")]
    public IReadOnlyCollection<string> Notes { get; set; } = [];

    [JsonPropertyName("steps")]
    public IReadOnlyCollection<PlanStepItem> Steps { get; set; } = [];
}

public sealed class PlanRiskItem
{
    [JsonPropertyName("risk")]
    public string Risk { get; set; } = string.Empty;

    [JsonPropertyName("mitigation")]
    public string? Mitigation { get; set; }
}

public sealed class PlanApprovalItem
{
    [JsonPropertyName("is_approved")]
    public bool IsApproved { get; set; }

    [JsonPropertyName("approval_timestamp")]
    public DateTimeOffset? ApprovalTimestamp { get; set; }
}

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

public sealed class ArtifactItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("intended_use")]
    public string IntendedUse { get; set; } = string.Empty;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("tags")]
    public IReadOnlyCollection<string> Tags { get; set; } = [];

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;
}

public sealed class SessionMetadataItem
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("current_state")]
    public string? CurrentState { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class LogItem
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;

    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; set; }
}
