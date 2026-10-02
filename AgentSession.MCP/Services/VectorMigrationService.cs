using System.Text.Json;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;
using AgentSession.MCP.Options;
using AgentSession.MCP.Observability;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

public sealed class VectorMigrationService(
    ManagedTransactionStore store,
    LearningCatalog catalog,
    CanonicalMemoryService memory,
    MemoryStoragePaths paths,
    OllamaEmbeddingClient embeddings,
    QdrantVectorIndex vectors,
    IOptions<QdrantOptions> qdrant,
    TimeProvider time,
    McpDependencyTelemetry telemetry
)
{
    internal static readonly ManagedFile ActiveFile = new(
        ManagedArea.Learning,
        null,
        ["indexes", "vector-active.json"]
    );

    internal Func<CancellationToken, Task>? BeforeAliasSwitch { get; set; }
    internal Action? AfterAliasSwitch { get; set; }

    public async Task<VectorCollectionIdentity?> GetActiveIdentityAsync(
        CancellationToken cancellationToken = default
    )
    {
        var active = await ReadActiveAsync(cancellationToken);
        if (active is null)
            return null;
        if (active.Active.RepositoryId != paths.RepositoryId)
            throw new InvalidDataException("Active vector collection repository is invalid.");
        return Identity(active.Active);
    }

    public Task<VectorMigrationResult> MigrateAsync(
        string operationId,
        CancellationToken cancellationToken = default
    ) => telemetry.TrackInternalAsync(
        "maintenance",
        "migration",
        null,
        token => MigrateCoreAsync(operationId, token),
        cancellationToken,
        EnrichMigration
    );

    private async Task<VectorMigrationResult> MigrateCoreAsync(
        string operationId,
        CancellationToken cancellationToken
    )
    {
        ManagedStoragePathResolver.RequireIdentifier(operationId);
        if (await RecoverCoreAsync(operationId, cancellationToken) is { } recovered)
            return recovered;
        var model = await embeddings.GetModelIdentityAsync(cancellationToken);
        var probe = await embeddings.EmbedAsync(
            ["agent memory embedding dimension probe"],
            cancellationToken: cancellationToken
        );
        var fingerprint = EmbeddingProjection.Fingerprint(
            "ollama",
            model.Model,
            model.Digest,
            probe.Vectors.Single().Length
        );
        var target = EmbeddingProjection.Collection(
            qdrant.Value.Collection,
            paths.RepositoryId,
            fingerprint
        );
        var active = await ReadActiveAsync(cancellationToken);
        var previousAlias = await vectors.GetAliasTargetAsync(target.Alias, cancellationToken);

        var built = await BuildStableAsync(target, cancellationToken);
        if (BeforeAliasSwitch is not null)
        {
            await BeforeAliasSwitch(cancellationToken);
            built = await BuildStableAsync(target, cancellationToken);
        }
        var intentFile = IntentFile(operationId);
        var intentBefore = await store.ReadAsync(intentFile, cancellationToken);
        var intent = new VectorMigrationIntent
        {
            OperationId = operationId,
            RepositoryId = paths.RepositoryId,
            Previous = active?.Active,
            PreviousPhysicalCollection = previousAlias,
            Target = Descriptor(target),
            CatalogGeneration = built.Generation,
            ExpectedPoints = built.Count,
            State = VectorMigrationState.Prepared,
            UpdatedAtUtc = time.GetUtcNow(),
        };
        await store.CommitAsync(
            "vector-migration-prepare-" + operationId,
            [
                new(
                    intentFile,
                    CanonicalMemoryService.Serialize(intent),
                    intentBefore is null ? null : ManagedTransactionStore.Hash(intentBefore)
                ),
            ],
            cancellationToken
        );

        await vectors.SwitchAliasAsync(target.Alias, target.PhysicalCollection, cancellationToken);
        AfterAliasSwitch?.Invoke();
        return await FinalizeAsync(intentFile, intent, active, recovered: false, cancellationToken);
    }

    public Task<VectorMigrationResult?> RecoverAsync(
        string operationId,
        CancellationToken cancellationToken = default
    ) => telemetry.TrackInternalAsync(
        "maintenance",
        "migration",
        null,
        token => RecoverCoreAsync(operationId, token),
        cancellationToken,
        static (activity, result) =>
        {
            activity.SetTag("mcp.recovery.performed", result?.Recovered == true);
            if (result is not null)
                activity.SetTag(
                    "mcp.work.indexed",
                    Math.Clamp(result.IndexedPoints, 0, 1_024)
                );
        }
    );

    private async Task<VectorMigrationResult?> RecoverCoreAsync(
        string operationId,
        CancellationToken cancellationToken
    )
    {
        ManagedStoragePathResolver.RequireIdentifier(operationId);
        var file = IntentFile(operationId);
        var json = await store.ReadAsync(file, cancellationToken);
        if (json is null)
            return null;
        var intent =
            JsonSerializer.Deserialize<VectorMigrationIntent>(json, MemoryJson.Options)
            ?? throw new InvalidDataException("Empty vector migration intent.");
        Validate(intent, operationId);
        if (intent.State == VectorMigrationState.Completed)
            return new(
                operationId,
                intent.Target.PhysicalCollection,
                intent.PreviousPhysicalCollection,
                intent.ExpectedPoints,
                true
            );
        var target = Identity(intent.Target);
        var aliasTarget = await vectors.GetAliasTargetAsync(target.Alias, cancellationToken);
        if (aliasTarget != target.PhysicalCollection)
            return null;
        await VerifyCompleteAsync(target, intent.ExpectedPoints, cancellationToken);
        return await FinalizeAsync(
            file,
            intent,
            await ReadActiveAsync(cancellationToken),
            recovered: true,
            cancellationToken
        );
    }

    private async Task<(long Generation, int Count)> BuildStableAsync(
        VectorCollectionIdentity target,
        CancellationToken cancellationToken
    )
    {
        await vectors.EnsureCollectionAsync(target, cancellationToken);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var snapshot = await catalog.ReadAsync(cancellationToken);
            var records = new List<MemoryRecord>();
            foreach (
                var entry in snapshot.Entries.Values
                    .Where(entry => entry.Tier == MemoryTier.Long)
                    .OrderBy(entry => entry.Id, StringComparer.Ordinal)
            )
            {
                cancellationToken.ThrowIfCancellationRequested();
                var record = await memory.ReadCanonicalAsync(entry, cancellationToken);
                var batch = await embeddings.EmbedAsync(
                    [EmbeddingProjection.Project(record)],
                    expectedDimensions: target.Fingerprint.Dimension,
                    cancellationToken
                );
                var projected = JsonSerializer.Deserialize<MemoryRecord>(
                    CanonicalMemoryService.Serialize(record),
                    MemoryJson.Options
                )!;
                projected.Embedding = new(
                    target.Fingerprint.Provider,
                    target.Fingerprint.Model,
                    target.Fingerprint.Dimension,
                    target.Fingerprint.ProjectionVersion,
                    time.GetUtcNow(),
                    target.Fingerprint.ModelDigest
                );
                projected.EmbeddingState = EmbeddingState.Ready;
                projected.IndexState = VectorIndexState.Indexed;
                var current = await vectors.GetPointStateAsync(
                    target,
                    paths.RepositoryId,
                    projected.Id,
                    cancellationToken
                );
                await vectors.UpsertAsync(
                    target,
                    projected,
                    batch.Vectors.Single(),
                    current?.Revision,
                    cancellationToken
                );
                records.Add(projected);
            }
            var after = await catalog.ReadAsync(cancellationToken);
            if (after.Manifest.Generation != snapshot.Manifest.Generation)
                continue;
            var canonicalIds = records.Select(record => record.Id).ToHashSet(StringComparer.Ordinal);
            var indexed = await vectors.ListPointStatesAsync(target, 100_001, cancellationToken);
            var orphanIds = indexed
                .Where(point => !canonicalIds.Contains(point.MemoryId))
                .Select(point => point.PointId)
                .ToArray();
            await vectors.DeletePointsAsync(target, orphanIds, cancellationToken);
            await VerifyCompleteAsync(target, records.Count, cancellationToken);
            return (snapshot.Manifest.Generation, records.Count);
        }
        throw new ValidationException(
            "Canonical learning changed repeatedly during vector migration.",
            "revision_conflict"
        );
    }

    private async Task VerifyCompleteAsync(
        VectorCollectionIdentity target,
        int expectedCount,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await catalog.ReadAsync(cancellationToken);
        var entries = snapshot.Entries.Values
            .Where(entry => entry.Tier == MemoryTier.Long)
            .ToArray();
        if (entries.Length != expectedCount)
            throw new ValidationException("Vector migration snapshot is stale.", "revision_conflict");
        foreach (var entry in entries)
        {
            var point = await vectors.GetPointStateAsync(
                target,
                paths.RepositoryId,
                entry.Id,
                cancellationToken
            );
            if (
                point is null
                || point.Revision != entry.Revision
                || point.ContentHash != entry.ContentHash
                || point.EmbeddingFingerprint != target.Fingerprint.Hash
            )
                throw new ValidationException(
                    "Vector migration completeness check failed.",
                    "index_incomplete"
                );
        }
        var indexed = await vectors.ListPointStatesAsync(
            target,
            Math.Max(1, expectedCount + 1),
            cancellationToken
        );
        if (indexed.Count != expectedCount)
            throw new ValidationException(
                "Vector migration contains missing or orphaned points.",
                "index_incomplete"
            );
    }

    private async Task<VectorMigrationResult> FinalizeAsync(
        ManagedFile intentFile,
        VectorMigrationIntent intent,
        ActiveVectorCollection? priorActive,
        bool recovered,
        CancellationToken cancellationToken
    )
    {
        var intentBefore = await store.ReadAsync(intentFile, cancellationToken)
            ?? throw new ValidationException("Migration intent disappeared.", "revision_conflict");
        intent.State = VectorMigrationState.Completed;
        intent.UpdatedAtUtc = time.GetUtcNow();
        var previous = priorActive?.Previous.ToList() ?? [];
        if (
            priorActive?.Active is { } prior
            && prior.PhysicalCollection != intent.Target.PhysicalCollection
            && previous.All(item => item.PhysicalCollection != prior.PhysicalCollection)
        )
            previous.Add(prior);
        var active = new ActiveVectorCollection
        {
            Active = intent.Target,
            Previous = previous,
            UpdatedAtUtc = time.GetUtcNow(),
        };
        var activeBefore = await store.ReadAsync(ActiveFile, cancellationToken);
        await store.CommitAsync(
            "vector-migration-finalize-" + intent.OperationId,
            [
                new(
                    ActiveFile,
                    CanonicalMemoryService.Serialize(active),
                    activeBefore is null ? null : ManagedTransactionStore.Hash(activeBefore)
                ),
                new(
                    intentFile,
                    CanonicalMemoryService.Serialize(intent),
                    ManagedTransactionStore.Hash(intentBefore)
                ),
            ],
            cancellationToken
        );
        return new(
            intent.OperationId,
            intent.Target.PhysicalCollection,
            intent.PreviousPhysicalCollection,
            intent.ExpectedPoints,
            recovered
        );
    }

    private async Task<ActiveVectorCollection?> ReadActiveAsync(
        CancellationToken cancellationToken
    )
    {
        var json = await store.ReadAsync(ActiveFile, cancellationToken);
        return json is null
            ? null
            : JsonSerializer.Deserialize<ActiveVectorCollection>(json, MemoryJson.Options)
                ?? throw new InvalidDataException("Empty active vector collection metadata.");
    }

    private static ManagedFile IntentFile(string operationId) =>
        new(
            ManagedArea.Learning,
            null,
            ["indexes", "vector-migrations", operationId + ".json"]
        );

    private static void EnrichMigration(
        System.Diagnostics.Activity activity,
        VectorMigrationResult result
    )
    {
        activity.SetTag("mcp.recovery.performed", result.Recovered);
        activity.SetTag("mcp.work.indexed", Math.Clamp(result.IndexedPoints, 0, 1_024));
    }

    private static VectorCollectionDescriptor Descriptor(VectorCollectionIdentity identity) =>
        new(
            identity.RepositoryId,
            identity.Alias,
            identity.PhysicalCollection,
            identity.Fingerprint
        );

    private static VectorCollectionIdentity Identity(VectorCollectionDescriptor descriptor) =>
        new(
            descriptor.RepositoryId,
            descriptor.Alias,
            descriptor.PhysicalCollection,
            descriptor.Fingerprint
        );

    private static void Validate(VectorMigrationIntent intent, string operationId)
    {
        if (
            intent.SchemaVersion != 1
            || intent.OperationId != operationId
            || intent.ExpectedPoints < 0
            || intent.CatalogGeneration < 0
            || intent.UpdatedAtUtc.Offset != TimeSpan.Zero
            || !Enum.IsDefined(intent.State)
        )
            throw new InvalidDataException("Invalid vector migration intent.");
        ManagedStoragePathResolver.RequireIdentifier(intent.RepositoryId);
        ManagedStoragePathResolver.RequireIdentifier(intent.Target.RepositoryId);
    }
}
