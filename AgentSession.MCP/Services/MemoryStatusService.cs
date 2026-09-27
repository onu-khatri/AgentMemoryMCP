using AgentSession.MCP.Contracts;

namespace AgentSession.MCP.Services;

public sealed class MemoryStatusService(
    CanonicalMemoryService memory,
    ManagedTransactionStore store,
    MemoryStoragePaths paths,
    MemoryMaintenanceState maintenance,
    OllamaEmbeddingClient embeddings,
    VectorMigrationService migration,
    QdrantVectorIndex vectors
)
{
    public async Task<MemoryStatusResult> GetAsync(
        CancellationToken cancellationToken = default
    )
    {
        var counts = await memory.ReadStatusSnapshotAsync(cancellationToken);
        var backlog = await store.GetRecoveryBacklogAsync(cancellationToken);
        var maintained = maintenance.Read();
        EmbeddingModelIdentity? model = null;
        DependencyStatus ollama;
        try
        {
            model = await embeddings.GetModelIdentityAsync(cancellationToken);
            ollama = new("healthy", Identity: model.Model + "@" + model.Digest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            ollama = new("degraded", "dependency_timeout");
        }
        catch (Exception error) when (error is HttpRequestException or InvalidDataException)
        {
            ollama = new(
                "degraded",
                error is HttpRequestException ? "dependency_unavailable" : "dependency_invalid"
            );
        }

        VectorCollectionIdentity? active = null;
        DependencyStatus qdrant;
        try
        {
            active = await migration.GetActiveIdentityAsync(cancellationToken);
            if (active is null)
                qdrant = new("degraded", "semantic_active_collection_missing");
            else if (await vectors.IsReadyAsync(active, cancellationToken))
                qdrant = new("healthy", Identity: active.PhysicalCollection);
            else
                qdrant = new("degraded", "vector_index_unavailable");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            qdrant = new(
                "degraded",
                error is InvalidDataException ? "index_invalid" : "dependency_unavailable"
            );
        }

        var indexHealth = counts.InvalidCanonicalRecords > 0
            ? "invalid_canonical_records"
            : counts.PendingEmbeddingRecords > 0
                ? "pending"
                : qdrant.State == "healthy"
                    ? "healthy"
                    : "degraded";
        return new(
            typeof(MemoryStatusService).Assembly.GetName().Version?.ToString() ?? "unknown",
            paths.RepositoryId,
            paths.Root,
            paths.LearningRoot,
            counts.TempRecords,
            counts.ExpiredTempRecords,
            counts.ShortActiveRecords,
            counts.ShortReviewDueRecords,
            counts.ShortCompactedRecords,
            counts.ShortArchivedRecords,
            counts.LongCandidateRecords,
            counts.LongValidatedRecords,
            counts.PendingEmbeddingRecords,
            backlog,
            ollama,
            qdrant,
            model?.Model ?? active?.Fingerprint.Model,
            active?.PhysicalCollection,
            indexHealth,
            maintained.LastCleanupAtUtc,
            maintained.LastIndexRepairAtUtc,
            maintained.LastEmbeddingRunAtUtc,
            maintained.LastReconciliationAtUtc,
            counts.LastCompactionAtUtc,
            counts.LastArchiveAtUtc,
            maintained.Deleted,
            maintained.Archived,
            maintained.Errors,
            maintained.VectorsProcessed,
            maintained.VectorsIndexed,
            maintained.VectorsQueued,
            maintained.OrphansDeleted
        );
    }
}
