using AgentSession.MCP.Contracts;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Options;
using AgentSession.MCP.Observability;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

public sealed class MemoryReindexService(
    LearningCatalog catalog,
    VectorIndexCoordinator coordinator,
    VectorMigrationService migration,
    IOptions<MemoryPolicyOptions> options,
    McpDependencyTelemetry telemetry
)
{
    public Task<ReindexMemoryResult> ReindexAsync(
        ReindexMemoryRequest request,
        CancellationToken cancellationToken = default
    ) => telemetry.TrackInternalAsync(
        "maintenance",
        "reindex",
        request.MaxItems,
        token => ReindexCoreAsync(request, token),
        cancellationToken,
        static (activity, result) =>
        {
            activity.SetTag(
                "mcp.work.processed",
                Math.Clamp(result.ProcessedVectors, 0, 1_024)
            );
            activity.SetTag(
                "mcp.work.indexed",
                Math.Clamp(result.IndexedVectors, 0, 1_024)
            );
            activity.SetTag(
                "mcp.work.deferred",
                Math.Clamp(result.DeferredVectors, 0, 1_024)
            );
            activity.SetTag("mcp.work.errors", Math.Clamp(result.Errors, 0, 1_024));
        }
    );

    private async Task<ReindexMemoryResult> ReindexCoreAsync(
        ReindexMemoryRequest request,
        CancellationToken cancellationToken
    )
    {
        ManagedStoragePathResolver.RequireIdentifier(request.OperationId);
        if (
            !Enum.IsDefined(request.Scope)
            || request.MaxItems < 1
            || request.MaxItems > options.Value.Limits.MaxMutationBatch
        )
            throw new ValidationException("Invalid reindex scope or bound.");
        var ids = request.MemoryIds ?? [];
        if (request.Scope == MemoryReindexScope.SpecificMemoryIds && ids.Count == 0)
            throw new ValidationException("Specific reindex requires memory IDs.");
        if (ids.Count > options.Value.Limits.MaxMutationBatch)
            throw new ValidationException("Reindex IDs exceed the configured bound.");
        foreach (var id in ids)
            ManagedStoragePathResolver.RequireIdentifier(id);

        var invalid = 0;
        var queued = 0;
        var orphans = 0;
        var processed = 0;
        var indexed = 0;
        var deferred = 0;
        var errors = 0;
        string? collection = null;
        var migrated = false;

        if (request.Scope is MemoryReindexScope.FileSystem or MemoryReindexScope.All)
            invalid = await catalog.RebuildAsync(cancellationToken);

        if (
            request.Scope
                is MemoryReindexScope.Qdrant
                    or MemoryReindexScope.ModelMigration
                    or MemoryReindexScope.All
        )
        {
            var result = await migration.MigrateAsync(request.OperationId, cancellationToken);
            collection = result.ActiveCollection;
            indexed = result.IndexedPoints;
            migrated = true;
        }

        if (request.Scope == MemoryReindexScope.SpecificMemoryIds)
        {
            var active = await migration.GetActiveIdentityAsync(cancellationToken)
                ?? throw new ValidationException(
                    "No active vector collection; run model migration first.",
                    "semantic_active_collection_missing"
                );
            var reconciliation = await coordinator.ReconcileSelectedAsync(
                active,
                ids,
                request.MaxItems,
                cancellationToken
            );
            queued += reconciliation.MissingOrStaleQueued;
            orphans += reconciliation.OrphansDeleted;
            errors += reconciliation.Errors;
            collection = active.PhysicalCollection;
        }

        if (
            request.Scope
                is MemoryReindexScope.PendingEmbeddings
                    or MemoryReindexScope.SpecificMemoryIds
                    or MemoryReindexScope.All
        )
        {
            var work = await coordinator.ProcessPendingAsync(request.MaxItems, cancellationToken);
            processed += work.Processed;
            indexed += work.Indexed;
            deferred += work.Deferred;
            errors += work.Errors;
        }

        return new(
            request.Scope,
            invalid,
            queued,
            orphans,
            processed,
            indexed,
            deferred,
            errors,
            collection,
            migrated
        );
    }
}
