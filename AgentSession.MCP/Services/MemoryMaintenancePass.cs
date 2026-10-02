using AgentSession.MCP.Options;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

internal interface IMemoryMaintenancePass
{
    Task<MemoryMaintenancePassResult> RunAsync(CancellationToken cancellationToken);
}

internal sealed record MemoryMaintenancePassResult(
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset? IndexRepairAtUtc,
    DateTimeOffset? EmbeddingRunAtUtc,
    DateTimeOffset? ReconciliationAtUtc,
    int Deleted,
    int Errors,
    int Archived,
    int VectorsProcessed,
    int VectorsIndexed,
    int VectorsDeferred,
    int VectorsQueued,
    int OrphansDeleted,
    string? ErrorCode
);

internal sealed class MemoryMaintenancePass(
    CanonicalMemoryService memory,
    IOptions<MemoryPolicyOptions> options,
    TimeProvider time,
    LearningCatalog catalog,
    VectorIndexCoordinator coordinator,
    VectorMigrationService migration
) : IMemoryMaintenancePass
{
    public async Task<MemoryMaintenancePassResult> RunAsync(
        CancellationToken cancellationToken
    )
    {
        var deleted = 0;
        var errors = 0;
        var archived = 0;
        var processed = 0;
        var indexed = 0;
        var deferred = 0;
        var queued = 0;
        var orphans = 0;
        DateTimeOffset? indexAt = null;
        DateTimeOffset? embeddingAt = null;
        DateTimeOffset? reconciliationAt = null;
        string? errorCode = null;

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(10));
        var jobToken = budget.Token;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            var result = await memory.RunMaintenanceAsync(jobToken);
            deleted += result.Deleted;
            archived += result.Archived;
            errors = result.Errors;
            if (
                result.Deleted < options.Value.Limits.MaxMutationBatch
                && result.Archived < options.Value.Limits.MaxMutationBatch
            )
                break;
        }

        await catalog.RebuildAsync(jobToken);
        indexAt = time.GetUtcNow();
        try
        {
            var work = await coordinator.ProcessPendingAsync(
                options.Value.Limits.MaxMutationBatch,
                jobToken
            );
            processed = work.Processed;
            indexed = work.Indexed;
            deferred = work.Deferred;
            errors += work.Errors;
            embeddingAt = time.GetUtcNow();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            errors++;
            errorCode = "embedding_job_degraded";
        }

        try
        {
            var active = await migration.GetActiveIdentityAsync(jobToken);
            if (active is not null)
            {
                var reconciliation = await coordinator.ReconcileAsync(
                    active,
                    options.Value.Limits.MaxMutationBatch,
                    jobToken
                );
                queued = reconciliation.MissingOrStaleQueued;
                orphans = reconciliation.OrphansDeleted;
                errors += reconciliation.Errors;
            }
            reconciliationAt = time.GetUtcNow();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            errors++;
            errorCode ??= "reconciliation_job_degraded";
        }

        return new(
            time.GetUtcNow(),
            indexAt,
            embeddingAt,
            reconciliationAt,
            deleted,
            errors,
            archived,
            processed,
            indexed,
            deferred,
            queued,
            orphans,
            errors == 0 ? null : errorCode ?? "maintenance_errors"
        );
    }
}
