using AgentSession.MCP.Helpers;

namespace AgentSession.MCP.Infrastructure.Observability;

internal sealed record DependencyFailureClassification(
    DependencyTelemetryOutcome Outcome,
    string? ErrorCode = null
);

internal static class DependencyFailureClassifier
{
    public static DependencyFailureClassification Success { get; } =
        new(DependencyTelemetryOutcome.Success);

    public static DependencyFailureClassification Classify(
        Exception error,
        CancellationToken callerCancellation,
        string dependencyType
    )
    {
        var classification = error switch
        {
            OperationCanceledException when callerCancellation.IsCancellationRequested =>
                new(DependencyTelemetryOutcome.ClientCancelled, "client_cancelled"),
            OperationCanceledException =>
                new(DependencyTelemetryOutcome.DeadlineExceeded, "deadline_exceeded"),
            TimeoutException =>
                new(DependencyTelemetryOutcome.DeadlineExceeded, "lock_timeout"),
            ValidationException validation => Classify(validation),
            HttpRequestException =>
                new(DependencyTelemetryOutcome.DependencyError, "dependency_http_error"),
            InvalidDataException =>
                new(DependencyTelemetryOutcome.ToolError, "storage_invalid"),
            IOException => new(DependencyTelemetryOutcome.ToolError, "storage_error"),
            _ when error.GetType().Namespace?.StartsWith("Grpc", StringComparison.Ordinal) == true =>
                new(DependencyTelemetryOutcome.DependencyError, "dependency_grpc_error"),
            _ => new(DependencyTelemetryOutcome.InternalError, "internal_error"),
        };

        if (
            classification.Outcome
            is DependencyTelemetryOutcome.ClientCancelled
                or DependencyTelemetryOutcome.DeadlineExceeded
                or DependencyTelemetryOutcome.ValidationError
                or DependencyTelemetryOutcome.AuthorizationDenied
        )
            return classification;

        if (dependencyType is not ("ollama" or "qdrant"))
            return classification;

        return new(
            DependencyTelemetryOutcome.DependencyError,
            dependencyType == "ollama" ? "dependency_http_error" : "dependency_grpc_error"
        );
    }

    private static DependencyFailureClassification Classify(ValidationException error)
    {
        var code = DependencyTelemetrySchema.NormalizeErrorCode(error.Code);
        return code switch
        {
            "approval_required" or "policy_denied" =>
                new(DependencyTelemetryOutcome.AuthorizationDenied, code),
            "storage_invalid" => new(DependencyTelemetryOutcome.ToolError, code),
            _ => new(DependencyTelemetryOutcome.ValidationError, code),
        };
    }
}
