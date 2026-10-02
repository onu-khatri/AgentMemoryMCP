using AgentSession.MCP.Helpers;
using ModelContextProtocol;

namespace AgentSession.MCP.Observability;

internal sealed record TelemetryClassification(
    TelemetryOutcome Outcome,
    string? ErrorCode = null,
    ValidationReason? ValidationReason = null,
    DenialReason? DenialReason = null
);

internal static class TelemetryOutcomeClassifier
{
    public static TelemetryClassification Success { get; } = new(TelemetryOutcome.Success);

    public static TelemetryClassification Classify(
        Exception error,
        CancellationToken callerCancellation = default
    ) => error switch
    {
        OperationCanceledException when callerCancellation.IsCancellationRequested =>
            new(TelemetryOutcome.ClientCancelled, "client_cancelled"),
        OperationCanceledException => new(TelemetryOutcome.DeadlineExceeded, "deadline_exceeded"),
        TimeoutException => new(TelemetryOutcome.DeadlineExceeded, "lock_timeout"),
        ValidationException validation => Classify(validation),
        HttpRequestException => new(TelemetryOutcome.DependencyError, "dependency_http_error"),
        InvalidDataException => new(TelemetryOutcome.ToolError, "storage_invalid"),
        McpProtocolException => new(TelemetryOutcome.ProtocolError, "protocol_error"),
        McpException => new(TelemetryOutcome.ToolError, "mcp_error"),
        IOException => new(TelemetryOutcome.ToolError, "storage_error"),
        _ when error.GetType().Namespace?.StartsWith("Grpc", StringComparison.Ordinal) == true =>
            new(TelemetryOutcome.DependencyError, "dependency_grpc_error"),
        _ => new(TelemetryOutcome.InternalError, "internal_error"),
    };

    public static TelemetryClassification Classify(ValidationException error)
    {
        var code = TelemetrySchema.NormalizeErrorCode(error.Code);
        return code switch
        {
            "approval_required" => new(
                TelemetryOutcome.AuthorizationDenied,
                code,
                DenialReason: Observability.DenialReason.ApprovalRequired
            ),
            "policy_denied" => new(
                TelemetryOutcome.AuthorizationDenied,
                code,
                DenialReason: Observability.DenialReason.PolicyDenied
            ),
            "capacity_exceeded" => new(
                TelemetryOutcome.ValidationError,
                code,
                ValidationReason.OutOfRange
            ),
            "storage_invalid" => new(TelemetryOutcome.ToolError, code),
            TelemetrySchema.Other => new(
                TelemetryOutcome.ValidationError,
                TelemetrySchema.Other,
                ValidationReason.Other
            ),
            _ => new(TelemetryOutcome.ValidationError, code, ValidationReason.DomainRule),
        };
    }
}
