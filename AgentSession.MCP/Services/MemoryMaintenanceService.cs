using System.Diagnostics;
using AgentSession.MCP.Observability;
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

internal sealed class MemoryMaintenanceService(
    IMemoryMaintenancePass maintenance,
    MemoryMaintenanceState state,
    IOptions<MemoryPolicyOptions> options,
    TimeProvider time,
    ILogger<MemoryMaintenanceService> logger
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
            using var activity = TelemetrySchema.Activities.StartActivity(
                "mcp operation maintenance maintenance",
                ActivityKind.Internal
            );
            activity?.SetTag("dependency.type", "maintenance");
            activity?.SetTag("dependency.operation", "maintenance");
            var classification = TelemetryOutcomeClassifier.Success;
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
            try
            {
                var result = await maintenance.RunAsync(stoppingToken);
                deleted = result.Deleted;
                archived = result.Archived;
                processed = result.VectorsProcessed;
                indexed = result.VectorsIndexed;
                deferred = result.VectorsDeferred;
                queued = result.VectorsQueued;
                orphans = result.OrphansDeleted;
                errors = result.Errors;
                errorCode = result.ErrorCode;
                indexAt = result.IndexRepairAtUtc;
                embeddingAt = result.EmbeddingRunAtUtc;
                reconciliationAt = result.ReconciliationAtUtc;
                state.Record(
                    new(
                        result.CompletedAtUtc,
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
                {
                    classification = new(
                        TelemetryOutcome.DependencyError,
                        "dependency_error"
                    );
                    logger.LogWarning(
                        "Memory cleanup retained {Count} records requiring operator review.",
                        errors
                    );
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                classification = new(
                    TelemetryOutcome.ClientCancelled,
                    "client_cancelled"
                );
                return;
            }
            catch (OperationCanceledException)
            {
                classification = new(
                    TelemetryOutcome.DeadlineExceeded,
                    "deadline_exceeded"
                );
                errorCode = "maintenance_budget_exceeded";
                errors++;
                state.Record(
                    new(
                        time.GetUtcNow(), indexAt, embeddingAt, reconciliationAt,
                        deleted, errors, archived, processed, indexed, queued, orphans, errorCode
                    )
                );
            }
            catch (Exception error)
            {
                classification = TelemetryOutcomeClassifier.Classify(error, stoppingToken);
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
            finally
            {
                activity?.SetTag("mcp.work.deleted", Math.Clamp(deleted, 0, 1_024));
                activity?.SetTag("mcp.work.archived", Math.Clamp(archived, 0, 1_024));
                activity?.SetTag("mcp.work.processed", Math.Clamp(processed, 0, 1_024));
                activity?.SetTag("mcp.work.indexed", Math.Clamp(indexed, 0, 1_024));
                activity?.SetTag("mcp.work.deferred", Math.Clamp(deferred, 0, 1_024));
                activity?.SetTag("mcp.work.queued", Math.Clamp(queued, 0, 1_024));
                activity?.SetTag("mcp.work.orphans", Math.Clamp(orphans, 0, 1_024));
                activity?.SetTag("mcp.work.errors", Math.Clamp(errors, 0, 1_024));
                activity?.SetTag(
                    "mcp.status",
                    TelemetrySchema.OutcomeName(classification.Outcome)
                );
                if (classification.Outcome != TelemetryOutcome.Success)
                {
                    activity?.SetTag(
                        "error.type",
                        TelemetrySchema.NormalizeErrorCode(classification.ErrorCode)
                    );
                    activity?.SetStatus(ActivityStatusCode.Error);
                }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
