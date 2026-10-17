using System.Diagnostics;
using AgentSession.MCP.Core.Observability;
using OnuObservability.Mcp.Health;

namespace AgentSession.MCP.Infrastructure.Observability;

public sealed class DependencyOperationTracker(IMcpDependencyFailureRecorder failures)
    : IDependencyOperationTracker
{
    public Task TrackAsync(
        DependencyOperation operation,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken
    ) =>
        TrackAsync<object?>(
            operation,
            async token =>
            {
                await action(token);
                return null;
            },
            cancellationToken
        );

    public async Task<T> TrackAsync<T>(
        DependencyOperation operation,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        Func<T, DependencyOperationMetadata?>? successMetadata = null
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(action);

        var activityKind = operation.Kind == DependencyOperationKind.External
            ? ActivityKind.Client
            : ActivityKind.Internal;
        var registered = DependencyTelemetrySchema.Resolve(
            activityKind,
            operation.DependencyType,
            operation.Operation
        );
        var classification = DependencyFailureClassifier.Success;
        using var activity = DependencyTelemetrySchema.Activities.StartActivity(
            activityKind == ActivityKind.Client
                ? $"mcp dependency {registered.Type} {registered.Operation}"
                : $"mcp operation {registered.Type} {registered.Operation}",
            activityKind
        );
        activity?.SetTag("dependency.type", registered.Type);
        activity?.SetTag("dependency.operation", registered.Operation);
        if (operation.ItemCount is { } count)
            activity?.SetTag("mcp.batch.items", Math.Clamp(count, 0, 1_024));

        try
        {
            var result = await action(cancellationToken);
            ApplyMetadata(activity, successMetadata?.Invoke(result));
            return result;
        }
        catch (Exception error)
        {
            classification = DependencyFailureClassifier.Classify(
                error,
                cancellationToken,
                registered.Type
            );
            if (
                operation.Kind == DependencyOperationKind.External
                && classification.Outcome == DependencyTelemetryOutcome.DependencyError
            )
                failures.Record(registered.Type, registered.Operation);
            throw;
        }
        finally
        {
            activity?.SetTag(
                "mcp.status",
                DependencyTelemetrySchema.OutcomeName(classification.Outcome)
            );
            if (classification.Outcome != DependencyTelemetryOutcome.Success)
            {
                activity?.SetTag(
                    "error.type",
                    DependencyTelemetrySchema.NormalizeErrorCode(classification.ErrorCode)
                );
                activity?.SetStatus(ActivityStatusCode.Error);
            }
        }
    }

    private static void ApplyMetadata(Activity? activity, DependencyOperationMetadata? metadata)
    {
        if (activity is null || metadata is null)
            return;

        SetCount(activity, "mcp.batch.items", metadata.BatchItems);
        SetCount(activity, "mcp.work.deleted", metadata.WorkDeleted);
        SetCount(activity, "mcp.work.archived", metadata.WorkArchived);
        SetCount(activity, "mcp.work.processed", metadata.WorkProcessed);
        SetCount(activity, "mcp.work.indexed", metadata.WorkIndexed);
        SetCount(activity, "mcp.work.deferred", metadata.WorkDeferred);
        SetCount(activity, "mcp.work.queued", metadata.WorkQueued);
        SetCount(activity, "mcp.work.orphans", metadata.WorkOrphans);
        SetCount(activity, "mcp.work.errors", metadata.WorkErrors);
        if (metadata.RecoveryPerformed is { } recoveryPerformed)
            activity.SetTag("mcp.recovery.performed", recoveryPerformed);
    }

    private static void SetCount(Activity activity, string name, int? value)
    {
        if (value is { } count)
            activity.SetTag(name, Math.Clamp(count, 0, 1_024));
    }
}
