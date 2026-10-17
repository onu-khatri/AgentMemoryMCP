using AgentSession.MCP.Helpers;
using System.Diagnostics;

namespace AgentSession.MCP.AgentMemory.Observability;

internal enum DependencyTelemetryOutcome
{
    Success,
    ClientCancelled,
    DeadlineExceeded,
    ValidationError,
    AuthorizationDenied,
    DependencyError,
    ToolError,
    InternalError,
}

internal sealed record TelemetryClassification(
    DependencyTelemetryOutcome Outcome,
    string? ErrorCode = null
);

internal static class DependencyTelemetrySchema
{
    public static ActivitySource Activities { get; } = new("AgentSession.MCP");

    public static string OutcomeName(DependencyTelemetryOutcome outcome) =>
        outcome switch
        {
            DependencyTelemetryOutcome.Success => "success",
            DependencyTelemetryOutcome.ClientCancelled => "client_cancelled",
            DependencyTelemetryOutcome.DeadlineExceeded => "deadline_exceeded",
            DependencyTelemetryOutcome.ValidationError => "validation_error",
            DependencyTelemetryOutcome.AuthorizationDenied => "authorization_denied",
            DependencyTelemetryOutcome.DependencyError => "dependency_error",
            DependencyTelemetryOutcome.ToolError => "tool_error",
            _ => "internal_error",
        };

    public static string NormalizeErrorCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? "other" : code.Trim().ToLowerInvariant();
}

internal static class TelemetryOutcomeClassifier
{
    public static TelemetryClassification Success { get; } = new(DependencyTelemetryOutcome.Success);

    public static TelemetryClassification Classify(
        Exception error,
        CancellationToken callerCancellation = default
    ) => error switch
    {
        OperationCanceledException when callerCancellation.IsCancellationRequested =>
            new(DependencyTelemetryOutcome.ClientCancelled, "client_cancelled"),
        OperationCanceledException =>
            new(DependencyTelemetryOutcome.DeadlineExceeded, "deadline_exceeded"),
        TimeoutException => new(DependencyTelemetryOutcome.DeadlineExceeded, "lock_timeout"),
        ValidationException validation => new(
            validation.Code == "approval_required" || validation.Code == "policy_denied"
                ? DependencyTelemetryOutcome.AuthorizationDenied
                : DependencyTelemetryOutcome.ValidationError,
            DependencyTelemetrySchema.NormalizeErrorCode(validation.Code)),
        HttpRequestException => new(DependencyTelemetryOutcome.DependencyError, "dependency_http_error"),
        InvalidDataException => new(DependencyTelemetryOutcome.ToolError, "storage_invalid"),
        IOException => new(DependencyTelemetryOutcome.ToolError, "storage_error"),
        _ when error.GetType().Namespace?.StartsWith("Grpc", StringComparison.Ordinal) == true =>
            new(DependencyTelemetryOutcome.DependencyError, "dependency_grpc_error"),
        _ => new(DependencyTelemetryOutcome.InternalError, "internal_error"),
    };
}
