using System.Collections.Frozen;
using System.Diagnostics;

namespace AgentSession.MCP.Observability;

/// <summary>
/// AgentMemory-owned dependency and maintenance telemetry compatibility schema.
/// Reusable MCP boundary, provider, exporter, and logging schemas live in OnuObservability.
/// </summary>
internal static class TelemetrySchema
{
    public const string ActivitySourceName = "AgentSession.MCP";
    public const string Other = "other";

    public static readonly ActivitySource Activities = new(ActivitySourceName);

    public static readonly FrozenSet<string> ErrorCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "approval_required",
        "capacity_exceeded",
        "claim_conflict",
        "client_cancelled",
        "content_policy_rejected",
        "deadline_exceeded",
        "dependency_grpc_error",
        "dependency_http_error",
        "dependency_error",
        "event_type_reserved",
        "index_incompatible",
        "index_incomplete",
        "internal_error",
        "invalid_request",
        "lock_timeout",
        "mcp_error",
        "not_found",
        "operation_conflict",
        "policy_denied",
        "protocol_error",
        "review_required",
        "revision_conflict",
        "source_coverage_incomplete",
        "stale_claim",
        "storage_error",
        "storage_invalid",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static string OutcomeName(TelemetryOutcome outcome) => ToMetricName(outcome.ToString());

    public static string NormalizeErrorCode(string? code) =>
        code is not null && ErrorCodes.Contains(code) ? code : Other;

    private static string ToMetricName(string value) =>
        string.Concat(value.Select((character, index) =>
            char.IsUpper(character) && index > 0
                ? "_" + char.ToLowerInvariant(character)
                : char.ToLowerInvariant(character).ToString()));
}

internal enum TelemetryOutcome
{
    Success,
    ValidationError,
    AuthorizationDenied,
    ClientCancelled,
    DeadlineExceeded,
    DependencyError,
    ToolError,
    ProtocolError,
    InternalError,
}

internal enum ValidationReason
{
    MissingRequired,
    WrongType,
    UnknownField,
    InvalidFormat,
    OutOfRange,
    DomainRule,
    Other,
}

internal enum DenialReason
{
    ApprovalRequired,
    PolicyDenied,
    Other,
}
