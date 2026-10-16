using System.Text.Json;
using AgentSession.MCP.Core.Observability;
using AgentSession.MCP.Core.Storage;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;
using AgentSession.MCP.Options;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

/// <summary>
/// Processes durable vector work without holding the repository lock across embedding or Qdrant calls.
/// </summary>
public sealed class VectorIndexCoordinator(
    IManagedTransactionStore store,
    LearningCatalog catalog,
    CanonicalMemoryService memory,
    IManagedStorageContext paths,
    OllamaEmbeddingClient embeddings,
    QdrantVectorIndex vectors,
    IOptions<QdrantOptions> qdrant,
    IOptions<MemoryPolicyOptions> policy,
    TimeProvider time,
    IDependencyOperationTracker telemetry
)
{
    public Task<VectorReconciliationResult> ReconcileAsync(
        VectorCollectionIdentity identity,
        int maxItems,
        CancellationToken cancellationToken = default
    ) => TrackReconciliationAsync(identity, maxItems, null, cancellationToken);

    public Task<VectorReconciliationResult> ReconcileSelectedAsync(
        VectorCollectionIdentity identity,
        IReadOnlyCollection<string> memoryIds,
        int maxItems,
        CancellationToken cancellationToken = default
    ) => TrackReconciliationAsync(identity, maxItems, memoryIds, cancellationToken);

    private Task<VectorReconciliationResult> TrackReconciliationAsync(
        VectorCollectionIdentity identity,
        int maxItems,
        IReadOnlyCollection<string>? memoryIds,
        CancellationToken cancellationToken
    ) => telemetry.TrackAsync(
        DependencyOperation.Internal("maintenance", "reconciliation", maxItems),
        token => ReconcileSelectedCoreAsync(identity, memoryIds, maxItems, token),
        cancellationToken,
        static result => new DependencyOperationMetadata
        {
            WorkQueued = result.MissingOrStaleQueued,
            WorkDeleted = result.OrphansDeleted,
            WorkErrors = result.Errors,
        }
    );

    private async Task<VectorReconciliationResult> ReconcileSelectedCoreAsync(
        VectorCollectionIdentity identity,
        IReadOnlyCollection<string>? memoryIds,
        int maxItems,
        CancellationToken cancellationToken
    )
    {
        if (memoryIds is not null)
        {
            if (memoryIds.Count == 0 || memoryIds.Count > policy.Value.Limits.MaxMutationBatch)
                throw new ValidationException("Specific reindex IDs exceed the configured bound.");
            foreach (var id in memoryIds)
                ManagedStorageIdentifier.RequireValid(id);
        }
        return await ReconcileCoreAsync(
            identity,
            maxItems,
            memoryIds?.ToHashSet(StringComparer.Ordinal),
            cancellationToken
        );
    }

    private async Task<VectorReconciliationResult> ReconcileCoreAsync(
        VectorCollectionIdentity identity,
        int maxItems,
        IReadOnlySet<string>? selectedIds,
        CancellationToken cancellationToken
    )
    {
        if (
            identity.RepositoryId != paths.RepositoryId
            || maxItems < 1
            || maxItems > policy.Value.Limits.MaxMutationBatch
        )
            throw new ValidationException("Invalid vector reconciliation request.");
        var snapshot = await catalog.ReadAsync(cancellationToken);
        var canonical = snapshot.Entries.Values
            .Where(entry =>
                entry.Tier == MemoryTier.Long
                && (selectedIds is null || selectedIds.Contains(entry.Id))
            )
            .OrderBy(entry => entry.Id, StringComparer.Ordinal)
            .Take(maxItems)
            .ToArray();
        var queued = 0;
        var errors = 0;
        foreach (var entry in canonical)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var point = await vectors.GetPointStateAsync(
                    identity,
                    paths.RepositoryId,
                    entry.Id,
                    cancellationToken
                );
                if (
                    point is not null
                    && point.Revision == entry.Revision
                    && point.ContentHash == entry.ContentHash
                    && point.EmbeddingFingerprint == identity.Fingerprint.Hash
                )
                    continue;
                var file = CanonicalMemoryService.VectorPendingFile(entry.Id);
                var before = await store.ReadAsync(file, cancellationToken);
                if (before is not null)
                {
                    try
                    {
                        var existing = JsonSerializer.Deserialize<VectorPendingWork>(
                            before,
                            MemoryJson.Options
                        );
                        if (
                            existing?.RepositoryId == paths.RepositoryId
                            && existing.MemoryId == entry.Id
                            && existing.Revision == entry.Revision
                            && existing.ContentHash == entry.ContentHash
                        )
                            continue;
                    }
                    catch (JsonException)
                    {
                        // Replace invalid derived pending work from canonical state.
                    }
                }
                var work = new VectorPendingWork
                {
                    WorkId = Guid.NewGuid().ToString("N"),
                    RepositoryId = paths.RepositoryId,
                    MemoryId = entry.Id,
                    Revision = entry.Revision,
                    ContentHash = entry.ContentHash,
                    CreatedAtUtc = time.GetUtcNow(),
                    NextAttemptAtUtc = time.GetUtcNow(),
                };
                var workJson = CanonicalMemoryService.Serialize(work);
                await store.CommitAsync(
                    "vector-reconcile-"
                        + ContentHash.Compute(
                            entry.Id
                                + "\n"
                                + entry.Revision
                                + "\n"
                                + identity.Fingerprint.Hash
                                + "\n"
                                + (before is null ? "new" : ContentHash.Compute(before))
                                + "\n"
                                + ContentHash.Compute(workJson)
                        )[..24],
                    [
                        new(
                            file,
                            workJson,
                            before is null ? null : ContentHash.Compute(before)
                        ),
                    ],
                    cancellationToken
                );
                queued++;
            }
            catch (ValidationException error) when (error.Code == "revision_conflict")
            {
                errors++;
            }
        }

        var canonicalIds = snapshot.Entries.Values
            .Where(entry => entry.Tier == MemoryTier.Long)
            .Select(entry => entry.Id)
            .ToHashSet(StringComparer.Ordinal);
        var indexed = await vectors.ListPointStatesAsync(identity, maxItems, cancellationToken);
        var orphanIds = indexed
            .Where(point => selectedIds is null && !canonicalIds.Contains(point.MemoryId))
            .Select(point => point.PointId)
            .ToArray();
        await vectors.DeletePointsAsync(identity, orphanIds, cancellationToken);
        return new(queued, orphanIds.Length, errors);
    }

    public Task<VectorWorkResult> ProcessPendingAsync(
        int maxItems,
        CancellationToken cancellationToken = default
    ) => telemetry.TrackAsync(
        DependencyOperation.Internal("maintenance", "maintenance", maxItems),
        token => ProcessPendingCoreAsync(maxItems, token),
        cancellationToken,
        static result => new DependencyOperationMetadata
        {
            WorkProcessed = result.Processed,
            WorkIndexed = result.Indexed,
            WorkDeferred = result.Deferred,
            WorkErrors = result.Errors,
        }
    );

    private async Task<VectorWorkResult> ProcessPendingCoreAsync(
        int maxItems,
        CancellationToken cancellationToken
    )
    {
        if (maxItems < 1 || maxItems > policy.Value.Limits.MaxMutationBatch)
            throw new ValidationException("Invalid vector work bound.");
        var documents = await store.ScanLearningDirectoryAsync(
            ["indexes", "vector-pending"],
            policy.Value.Limits.MaxMutationBatch,
            cancellationToken
        );
        var processed = 0;
        var indexed = 0;
        var deferred = 0;
        var errors = 0;
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VectorPendingWork pending;
            try
            {
                pending =
                    JsonSerializer.Deserialize<VectorPendingWork>(
                        document.Content,
                        MemoryJson.Options
                    ) ?? throw new JsonException("Empty vector work item.");
                Validate(pending, document.File);
            }
            catch (Exception error) when (error is JsonException or ValidationException)
            {
                errors++;
                continue;
            }
            if (pending.NextAttemptAtUtc > time.GetUtcNow())
            {
                deferred++;
                continue;
            }
            if (processed >= maxItems)
                break;
            processed++;
            try
            {
                await ProcessOneAsync(document, pending, cancellationToken);
                indexed++;
            }
            catch (ValidationException error) when (error.Code == "revision_conflict")
            {
                // A newer canonical write replaced this work item while the network calls ran.
                deferred++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                errors++;
                if (await DeferAsync(document, pending, ErrorCode(error), cancellationToken))
                    deferred++;
            }
        }
        return new(processed, indexed, deferred, errors);
    }

    private async Task ProcessOneAsync(
        ManagedDocument document,
        VectorPendingWork pending,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await catalog.ReadAsync(cancellationToken);
        if (
            !snapshot.Entries.TryGetValue(pending.MemoryId, out var entry)
            || entry.Tier != MemoryTier.Long
        )
        {
            await store.CommitAsync(
                "vector-drop-" + ContentHash.Compute(document.Content)[..24],
                [new(document.File, null, ContentHash.Compute(document.Content))],
                cancellationToken
            );
            return;
        }
        var record = await memory.ReadCanonicalAsync(entry, cancellationToken);
        var canonicalJson = await store.ReadAsync(LearningCatalog.FileFor(record), cancellationToken)
            ?? throw new ValidationException("Canonical memory disappeared.", "revision_conflict");
        var model = await embeddings.GetModelIdentityAsync(cancellationToken);
        var batch = await embeddings.EmbedAsync(
            [EmbeddingProjection.Project(record)],
            cancellationToken: cancellationToken
        );
        var vector = batch.Vectors.Single();
        var fingerprint = EmbeddingProjection.Fingerprint(
            "ollama",
            model.Model,
            model.Digest,
            vector.Length
        );
        var collection = EmbeddingProjection.Collection(
            qdrant.Value.Collection,
            paths.RepositoryId,
            fingerprint
        );
        var indexedRecord = JsonSerializer.Deserialize<MemoryRecord>(
            canonicalJson,
            MemoryJson.Options
        )!;
        indexedRecord.Embedding = new(
            fingerprint.Provider,
            fingerprint.Model,
            fingerprint.Dimension,
            fingerprint.ProjectionVersion,
            time.GetUtcNow(),
            fingerprint.ModelDigest
        );
        indexedRecord.EmbeddingState = EmbeddingState.Ready;
        indexedRecord.IndexState = VectorIndexState.Indexed;
        MemoryRecordValidator.Validate(indexedRecord);

        var point = await vectors.GetPointStateAsync(
            collection,
            paths.RepositoryId,
            record.Id,
            cancellationToken
        );
        await vectors.UpsertAsync(
            collection,
            indexedRecord,
            vector,
            point?.Revision,
            cancellationToken
        );

        var currentPending = await store.ReadAsync(document.File, cancellationToken);
        if (currentPending != document.Content)
            throw new ValidationException("Vector work was replaced.", "revision_conflict");
        var currentSnapshot = await catalog.ReadAsync(cancellationToken);
        if (
            !currentSnapshot.Entries.TryGetValue(record.Id, out var currentEntry)
            || currentEntry.Revision != record.Revision
            || currentEntry.ContentHash != record.ContentHash
        )
            throw new ValidationException("Canonical memory changed.", "revision_conflict");
        await store.CommitAsync(
            "vector-complete-"
                + ContentHash.Compute(
                    pending.WorkId + "\n" + fingerprint.Hash
                )[..24],
            [
                new(
                    LearningCatalog.FileFor(indexedRecord),
                    CanonicalMemoryService.Serialize(indexedRecord),
                    ContentHash.Compute(canonicalJson)
                ),
                .. LearningCatalog.Update(
                    currentSnapshot,
                    [LearningCatalog.EntryFor(indexedRecord)],
                    []
                ),
                new(document.File, null, ContentHash.Compute(document.Content)),
            ],
            cancellationToken
        );
    }

    private async Task<bool> DeferAsync(
        ManagedDocument document,
        VectorPendingWork pending,
        string errorCode,
        CancellationToken cancellationToken
    )
    {
        pending.Attempts = Math.Min(pending.Attempts + 1, 1_000_000);
        pending.LastErrorCode = errorCode;
        var delaySeconds = Math.Min(300, Math.Pow(2, Math.Min(pending.Attempts - 1, 8)));
        pending.NextAttemptAtUtc = time.GetUtcNow().AddSeconds(delaySeconds);
        try
        {
            await store.CommitAsync(
                "vector-defer-"
                    + ContentHash.Compute(
                        ContentHash.Compute(document.Content) + "\n" + errorCode
                    )[..24],
                [
                    new(
                        document.File,
                        CanonicalMemoryService.Serialize(pending),
                        ContentHash.Compute(document.Content)
                    ),
                ],
                cancellationToken
            );
            return true;
        }
        catch (ValidationException error) when (error.Code == "revision_conflict")
        {
            return false;
        }
    }

    private static void Validate(VectorPendingWork pending, ManagedFile file)
    {
        if (
            pending.SchemaVersion != 1
            || pending.WorkId.Length == 0
            || pending.Revision < 1
            || pending.Attempts < 0
            || pending.RepositoryId.Length == 0
            || pending.ContentHash.Length == 0
            || pending.CreatedAtUtc.Offset != TimeSpan.Zero
            || pending.NextAttemptAtUtc.Offset != TimeSpan.Zero
            || Path.GetFileNameWithoutExtension(file.Parts[^1]) != pending.MemoryId
        )
            throw new ValidationException("Invalid vector work item.", "storage_invalid");
        ManagedStorageIdentifier.RequireValid(pending.RepositoryId);
        ManagedStorageIdentifier.RequireValid(pending.MemoryId);
        ManagedStorageIdentifier.RequireValid(pending.WorkId);
    }

    private static string ErrorCode(Exception error) =>
        error switch
        {
            TimeoutException => "dependency_timeout",
            HttpRequestException => "dependency_unavailable",
            InvalidDataException => "dependency_invalid",
            ValidationException validation => validation.Code,
            _ => "vector_processing_failed",
        };
}
