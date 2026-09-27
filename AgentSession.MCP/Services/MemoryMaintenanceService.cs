using System.Diagnostics;
using AgentSession.MCP.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

public sealed class MemoryMaintenanceState
{
    private readonly object _gate = new();
    private MaintenanceSnapshot _snapshot = new(
        null, null, null, null, 0, 0, 0, 0, 0, 0, 0, null
    );

    public MaintenanceSnapshot Read()
    {
        lock (_gate)
            return _snapshot;
    }

    public void Record(MaintenanceSnapshot value)
    {
        lock (_gate)
            _snapshot = value;
    }
}

public sealed record MaintenanceSnapshot(
    DateTimeOffset? LastCleanupAtUtc,
    DateTimeOffset? LastIndexRepairAtUtc,
    DateTimeOffset? LastEmbeddingRunAtUtc,
    DateTimeOffset? LastReconciliationAtUtc,
    int Deleted,
    int Errors,
    int Archived,
    int VectorsProcessed,
    int VectorsIndexed,
    int VectorsQueued,
    int OrphansDeleted,
    string? ErrorCode
);

public sealed class MemoryMaintenanceService(
    CanonicalMemoryService memory,
    MemoryMaintenanceState state,
    IOptions<MemoryPolicyOptions> options,
    TimeProvider time,
    ILogger<MemoryMaintenanceService> logger,
    LearningCatalog catalog,
    VectorIndexCoordinator coordinator,
    VectorMigrationService migration
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(options.Value.Temp.CleanupIntervalMinutes),
            time
        );
        do
        {
            var deleted = 0;
            var errors = 0;
            var archived = 0;
            var processed = 0;
            var indexed = 0;
            var queued = 0;
            var orphans = 0;
            DateTimeOffset? indexAt = null;
            DateTimeOffset? embeddingAt = null;
            DateTimeOffset? reconciliationAt = null;
            string? errorCode = null;
            try
            {
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                budget.CancelAfter(TimeSpan.FromSeconds(10));
                var jobToken = budget.Token;
                var watch = Stopwatch.StartNew();
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
                state.Record(
                    new(
                        time.GetUtcNow(),
                        indexAt,
                        embeddingAt,
                        reconciliationAt,
                        deleted,
                        errors,
                        archived,
                        processed,
                        indexed,
                        queued,
                        orphans,
                        errors == 0 ? null : errorCode ?? "maintenance_errors"
                    )
                );
                if (errors > 0)
                    logger.LogWarning(
                        "Memory cleanup retained {Count} records requiring operator review.",
                        errors
                    );
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                errorCode = "maintenance_budget_exceeded";
                errors++;
                state.Record(
                    new(
                        time.GetUtcNow(), indexAt, embeddingAt, reconciliationAt,
                        deleted, errors, archived, processed, indexed, queued, orphans, errorCode
                    )
                );
            }
            catch (Exception)
            {
                state.Record(
                    new(
                        time.GetUtcNow(), indexAt, embeddingAt, reconciliationAt,
                        deleted, errors + 1, archived, processed, indexed, queued, orphans,
                        "cleanup_failed"
                    )
                );
                logger.LogWarning(
                    "Memory cleanup failed; canonical data requires review. No content is included in this diagnostic."
                );
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
