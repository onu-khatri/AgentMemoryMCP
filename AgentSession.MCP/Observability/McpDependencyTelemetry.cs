using System.Diagnostics;
using OnuObservability.Mcp;

namespace AgentSession.MCP.Observability;

public sealed class McpDependencyTelemetry(IMcpDependencyFailureRecorder failures)
{
    public Task TrackAsync(
        string dependencyType,
        string operation,
        int? itemCount,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken
    ) =>
        TrackCoreAsync<object?>(
            ActivityKind.Client,
            dependencyType,
            operation,
            itemCount,
            async token =>
            {
                await action(token);
                return null;
            },
            cancellationToken
        );

    public async Task<T> TrackAsync<T>(
        string dependencyType,
        string operation,
        int? itemCount,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken
    ) => await TrackCoreAsync(
        ActivityKind.Client,
        dependencyType,
        operation,
        itemCount,
        action,
        cancellationToken
    );

    internal Task TrackInternalAsync(
        string dependencyType,
        string operation,
        int? itemCount,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken
    ) => TrackCoreAsync<object?>(
        ActivityKind.Internal,
        dependencyType,
        operation,
        itemCount,
        async token =>
        {
            await action(token);
            return null;
        },
        cancellationToken
    );

    internal Task<T> TrackInternalAsync<T>(
        string dependencyType,
        string operation,
        int? itemCount,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        Action<Activity, T>? enrichOnSuccess = null
    ) => TrackCoreAsync(
        ActivityKind.Internal,
        dependencyType,
        operation,
        itemCount,
        action,
        cancellationToken,
        enrichOnSuccess
    );

    private async Task<T> TrackCoreAsync<T>(
        ActivityKind activityKind,
        string dependencyType,
        string operation,
        int? itemCount,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        Action<Activity, T>? enrichOnSuccess = null
    )
    {
        var registered = AgentMemoryTelemetrySchema.Resolve(
            activityKind,
            dependencyType,
            operation);
        dependencyType = registered.Type;
        operation = registered.Operation;
        var classification = TelemetryOutcomeClassifier.Success;
        using var activity = TelemetrySchema.Activities.StartActivity(
            activityKind == ActivityKind.Client
                ? $"mcp dependency {dependencyType} {operation}"
                : $"mcp operation {dependencyType} {operation}",
            activityKind
        );
        activity?.SetTag("dependency.type", dependencyType);
        activity?.SetTag("dependency.operation", operation);
        if (itemCount is { } count)
            activity?.SetTag("mcp.batch.items", Math.Clamp(count, 0, 1_024));

        try
        {
            var result = await action(cancellationToken);
            if (activity is not null)
                enrichOnSuccess?.Invoke(activity, result);
            return result;
        }
        catch (Exception error)
        {
            classification = ClassifyDependencyFailure(error, cancellationToken, dependencyType);
            if (classification.Outcome == TelemetryOutcome.DependencyError)
                failures.Record(dependencyType, operation);
            throw;
        }
        finally
        {
            var outcome = TelemetrySchema.OutcomeName(classification.Outcome);
            activity?.SetTag("mcp.status", outcome);
            if (classification.Outcome != TelemetryOutcome.Success)
            {
                activity?.SetTag(
                    "error.type",
                    TelemetrySchema.NormalizeErrorCode(classification.ErrorCode)
                );
                activity?.SetStatus(ActivityStatusCode.Error);
            }
        }
    }

    private static TelemetryClassification ClassifyDependencyFailure(
        Exception error,
        CancellationToken cancellationToken,
        string dependencyType
    )
    {
        var classification = TelemetryOutcomeClassifier.Classify(error, cancellationToken);
        if (
            classification.Outcome
            is TelemetryOutcome.ClientCancelled
                or TelemetryOutcome.DeadlineExceeded
                or TelemetryOutcome.ValidationError
                or TelemetryOutcome.AuthorizationDenied
        )
            return classification;

        if (dependencyType is not ("ollama" or "qdrant"))
            return classification;

        return new(
            TelemetryOutcome.DependencyError,
            dependencyType switch
            {
                "ollama" => "dependency_http_error",
                "qdrant" => "dependency_grpc_error",
                _ => "dependency_error",
            }
        );
    }

}
