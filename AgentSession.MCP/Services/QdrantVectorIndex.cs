using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Models.Memory;
using AgentSession.MCP.Observability;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

namespace AgentSession.MCP.Services;

public sealed record VectorSearchFilter(
    string? Status = null,
    string? Category = null,
    string? Tag = null,
    IReadOnlyList<string>? Statuses = null,
    IReadOnlyList<string>? Categories = null,
    IReadOnlyList<string>? Tags = null,
    string? DecisionArea = null
);

public sealed record VectorSearchHit(
    Guid PointId,
    string MemoryId,
    long Revision,
    string ContentHash,
    string Status,
    float Score
);

public sealed record VectorPointState(
    long Revision,
    string ContentHash,
    string EmbeddingFingerprint,
    string PayloadHash
);

public sealed record IndexedVectorPoint(Guid PointId, string MemoryId, VectorPointState State);

/// <summary>
/// Repository-filtered, rebuildable Qdrant index for canonical long-term records.
/// Canonical files remain authoritative; this type never persists record content.
/// </summary>
public sealed class QdrantVectorIndex(
    QdrantClient client,
    IMemoryContentPolicy contentPolicy,
    McpDependencyTelemetry telemetry
)
{
    private static readonly (string Name, PayloadSchemaType Type)[] PayloadIndexes =
    [
        ("repositoryId", PayloadSchemaType.Keyword),
        ("memoryId", PayloadSchemaType.Keyword),
        ("status", PayloadSchemaType.Keyword),
        ("category", PayloadSchemaType.Keyword),
        ("decisionArea", PayloadSchemaType.Keyword),
        ("tags", PayloadSchemaType.Keyword),
        ("sourceType", PayloadSchemaType.Keyword),
        ("contentHash", PayloadSchemaType.Keyword),
        ("embeddingProvider", PayloadSchemaType.Keyword),
        ("embeddingModel", PayloadSchemaType.Keyword),
        ("embeddingVersion", PayloadSchemaType.Keyword),
        ("embeddingFingerprint", PayloadSchemaType.Keyword),
        ("payloadHash", PayloadSchemaType.Keyword),
        ("revision", PayloadSchemaType.Integer),
        ("confidence", PayloadSchemaType.Float),
        ("createdAtUtc", PayloadSchemaType.Datetime),
        ("updatedAtUtc", PayloadSchemaType.Datetime),
        ("validatedAtUtc", PayloadSchemaType.Datetime),
    ];

    public static Guid PointId(string repositoryId, string memoryId)
    {
        ManagedStoragePathResolver.RequireIdentifier(repositoryId);
        ManagedStoragePathResolver.RequireIdentifier(memoryId);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(repositoryId + "\n" + memoryId));
        Span<byte> uuid = stackalloc byte[16];
        digest.AsSpan(0, uuid.Length).CopyTo(uuid);
        uuid[6] = (byte)((uuid[6] & 0x0f) | 0x50); // deterministic UUID v5 shape
        uuid[8] = (byte)((uuid[8] & 0x3f) | 0x80);
        return new Guid(uuid, bigEndian: true);
    }

    internal Task<T> TrackOperationAsync<T>(
        string dependencyType,
        string operation,
        int? itemCount,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken
    ) => telemetry.TrackAsync(
        dependencyType,
        operation,
        itemCount,
        action,
        cancellationToken
    );

    internal Task TrackOperationAsync(
        string dependencyType,
        string operation,
        int? itemCount,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken
    ) => telemetry.TrackAsync(
        dependencyType,
        operation,
        itemCount,
        action,
        cancellationToken
    );

    public Task<bool> IsReadyAsync(
        VectorCollectionIdentity identity,
        CancellationToken cancellationToken = default
    ) =>
        TrackOperationAsync(
            "qdrant",
            "health",
            null,
            token => IsReadyCoreAsync(identity, token),
            cancellationToken
        );

    private async Task<bool> IsReadyCoreAsync(
        VectorCollectionIdentity identity,
        CancellationToken cancellationToken = default
    )
    {
        if (!await client.CollectionExistsAsync(identity.PhysicalCollection, cancellationToken))
            return false;
        var alias = await GetAliasTargetCoreAsync(identity.Alias, cancellationToken);
        if (alias != identity.PhysicalCollection)
            return false;
        var info = await client.GetCollectionInfoAsync(
            identity.PhysicalCollection,
            cancellationToken
        );
        var vectors = info.Config?.Params?.VectorsConfig;
        var fingerprint = info.Config?.Metadata.TryGetValue(
            "embeddingFingerprint",
            out var value
        ) == true
            ? value.StringValue
            : null;
        return vectors?.Params is not null
            && vectors.Params.Size == (ulong)identity.Fingerprint.Dimension
            && vectors.Params.Distance == Distance.Cosine
            && fingerprint == identity.Fingerprint.Hash;
    }

    public Task EnsureCollectionAsync(
        VectorCollectionIdentity identity,
        CancellationToken cancellationToken = default
    ) =>
        TrackOperationAsync(
            "qdrant",
            "collection",
            null,
            token => EnsureCollectionCoreAsync(identity, token),
            cancellationToken
        );

    private async Task EnsureCollectionCoreAsync(
        VectorCollectionIdentity identity,
        CancellationToken cancellationToken = default
    )
    {
        if (!await client.CollectionExistsAsync(identity.PhysicalCollection, cancellationToken))
        {
            try
            {
                await client.CreateCollectionAsync(
                    collectionName: identity.PhysicalCollection,
                    vectorsConfig: new VectorParams
                    {
                        Size = (ulong)identity.Fingerprint.Dimension,
                        Distance = Distance.Cosine,
                    },
                    metadata: new Dictionary<string, Value>
                    {
                        ["embeddingFingerprint"] = identity.Fingerprint.Hash,
                        ["embeddingProvider"] = identity.Fingerprint.Provider,
                        ["embeddingModel"] = identity.Fingerprint.Model,
                        ["embeddingVersion"] = identity.Fingerprint.ProjectionVersion,
                    },
                    cancellationToken: cancellationToken
                );
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                if (
                    !await client.CollectionExistsAsync(
                        identity.PhysicalCollection,
                        cancellationToken
                    )
                )
                    throw;
                // Another process created the same deterministic collection.
            }
        }

        var info = await client.GetCollectionInfoAsync(
            identity.PhysicalCollection,
            cancellationToken
        );
        var vectors = info.Config?.Params?.VectorsConfig;
        var storedFingerprint = info.Config?.Metadata.TryGetValue(
            "embeddingFingerprint",
            out var value
        ) == true
            ? value.StringValue
            : null;
        if (
            vectors?.Params is null
            || vectors.Params.Size != (ulong)identity.Fingerprint.Dimension
            || vectors.Params.Distance != Distance.Cosine
            || !string.Equals(
                storedFingerprint,
                identity.Fingerprint.Hash,
                StringComparison.Ordinal
            )
        )
            throw new ValidationException(
                "Qdrant collection is incompatible with the configured embedding fingerprint.",
                "index_incompatible"
            );

        foreach (var (name, type) in PayloadIndexes)
        {
            if (info.PayloadSchema.TryGetValue(name, out var schema) && schema.DataType == type)
                continue;
            await client.CreatePayloadIndexAsync(
                identity.PhysicalCollection,
                name,
                type,
                wait: true,
                cancellationToken: cancellationToken
            );
        }
    }

    public Task UpsertAsync(
        VectorCollectionIdentity identity,
        MemoryRecord record,
        ReadOnlyMemory<float> vector,
        long? expectedIndexedRevision = null,
        CancellationToken cancellationToken = default
    ) =>
        TrackOperationAsync(
            "qdrant",
            "upsert",
            1,
            token => UpsertCoreAsync(identity, record, vector, expectedIndexedRevision, token),
            cancellationToken
        );

    private async Task UpsertCoreAsync(
        VectorCollectionIdentity identity,
        MemoryRecord record,
        ReadOnlyMemory<float> vector,
        long? expectedIndexedRevision = null,
        CancellationToken cancellationToken = default
    )
    {
        ValidateRecord(identity, record, vector.Span);
        await EnsureCollectionCoreAsync(identity, cancellationToken);
        var pointId = PointId(record.RepositoryId, record.Id);
        var existing = await client.RetrieveAsync(
            identity.PhysicalCollection,
            pointId,
            withPayload: true,
            withVectors: false,
            cancellationToken: cancellationToken
        );
        var current = existing.SingleOrDefault();
        var payload = CreatePayload(identity, record);
        var expectedPayloadHash = RequiredString(payload, "payloadHash");
        if (current is not null)
        {
            var currentRevision = RequiredInteger(current.Payload, "revision");
            var currentHash = RequiredString(current.Payload, "contentHash");
            var currentFingerprint = RequiredString(current.Payload, "embeddingFingerprint");
            var currentPayloadHash = RequiredString(current.Payload, "payloadHash");
            if (
                currentRevision == record.Revision
                && currentHash == record.ContentHash
                && currentFingerprint == identity.Fingerprint.Hash
                && currentPayloadHash == expectedPayloadHash
            )
                return;
            if (
                expectedIndexedRevision is null
                || expectedIndexedRevision.Value != currentRevision
                || record.Revision <= currentRevision
            )
                throw RevisionConflict();
        }
        else if (expectedIndexedRevision is > 0)
        {
            throw RevisionConflict();
        }

        contentPolicy.Validate(payload.ToDictionary(pair => pair.Key, pair => ToPolicyValue(pair.Value)));
        var point = new PointStruct
        {
            Id = pointId,
            Vectors = vector.ToArray(),
        };
        point.Payload.Add(payload);

        var request = new UpsertPoints
        {
            CollectionName = identity.PhysicalCollection,
            Wait = true,
            UpdateMode = current is null ? UpdateMode.InsertOnly : UpdateMode.UpdateOnly,
        };
        request.Points.Add(point);
        if (current is not null)
        {
            request.UpdateFilter = new Filter();
            request.UpdateFilter.Must.Add(MatchKeyword("repositoryId", record.RepositoryId));
            request.UpdateFilter.Must.Add(MatchKeyword("memoryId", record.Id));
            request.UpdateFilter.Must.Add(Match("revision", expectedIndexedRevision!.Value));
        }
        await client.UpsertAsync(request, cancellationToken);

        var committed = (
            await client.RetrieveAsync(
                identity.PhysicalCollection,
                pointId,
                withPayload: true,
                withVectors: false,
                cancellationToken: cancellationToken
            )
        ).SingleOrDefault();
        if (
            committed is null
            || RequiredInteger(committed.Payload, "revision") != record.Revision
            || RequiredString(committed.Payload, "contentHash") != record.ContentHash
            || RequiredString(committed.Payload, "embeddingFingerprint")
                != identity.Fingerprint.Hash
            || RequiredString(committed.Payload, "payloadHash") != expectedPayloadHash
        )
            throw RevisionConflict();
    }

    public Task<VectorPointState?> GetPointStateAsync(
        VectorCollectionIdentity identity,
        string repositoryId,
        string memoryId,
        CancellationToken cancellationToken = default
    ) =>
        TrackOperationAsync(
            "qdrant",
            "query",
            1,
            token => GetPointStateCoreAsync(identity, repositoryId, memoryId, token),
            cancellationToken
        );

    private async Task<VectorPointState?> GetPointStateCoreAsync(
        VectorCollectionIdentity identity,
        string repositoryId,
        string memoryId,
        CancellationToken cancellationToken = default
    )
    {
        if (!string.Equals(repositoryId, identity.RepositoryId, StringComparison.Ordinal))
            throw new ValidationException("Repository does not match the vector collection.");
        if (!await client.CollectionExistsAsync(identity.PhysicalCollection, cancellationToken))
            return null;
        var points = await client.RetrieveAsync(
            identity.PhysicalCollection,
            PointId(repositoryId, memoryId),
            withPayload: true,
            withVectors: false,
            cancellationToken: cancellationToken
        );
        var point = points.SingleOrDefault();
        if (point is null)
            return null;
        if (RequiredString(point.Payload, "repositoryId") != repositoryId)
            throw new InvalidDataException("Qdrant point repository payload is invalid.");
        return new(
            RequiredInteger(point.Payload, "revision"),
            RequiredString(point.Payload, "contentHash"),
            RequiredString(point.Payload, "embeddingFingerprint"),
            RequiredString(point.Payload, "payloadHash")
        );
    }

    public Task<IReadOnlyList<IndexedVectorPoint>> ListPointStatesAsync(
        VectorCollectionIdentity identity,
        int limit,
        CancellationToken cancellationToken = default
    ) =>
        TrackOperationAsync(
            "qdrant",
            "query",
            limit,
            token => ListPointStatesCoreAsync(identity, limit, token),
            cancellationToken
        );

    private async Task<IReadOnlyList<IndexedVectorPoint>> ListPointStatesCoreAsync(
        VectorCollectionIdentity identity,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        if (limit < 1)
            throw new ValidationException("Invalid vector listing bound.");
        if (!await client.CollectionExistsAsync(identity.PhysicalCollection, cancellationToken))
            return [];
        var filter = new Filter();
        filter.Must.Add(MatchKeyword("repositoryId", identity.RepositoryId));
        var points = new List<IndexedVectorPoint>();
        PointId? offset = null;
        while (points.Count < limit)
        {
            var page = await client.ScrollAsync(
                identity.PhysicalCollection,
                filter: filter,
                limit: (uint)Math.Min(100, limit - points.Count),
                offset: offset,
                payloadSelector: true,
                vectorsSelector: false,
                cancellationToken: cancellationToken
            );
            foreach (var point in page.Result)
            {
                points.Add(
                    new(
                        Guid.Parse(point.Id.Uuid),
                        RequiredString(point.Payload, "memoryId"),
                        new(
                            RequiredInteger(point.Payload, "revision"),
                            RequiredString(point.Payload, "contentHash"),
                            RequiredString(point.Payload, "embeddingFingerprint"),
                            RequiredString(point.Payload, "payloadHash")
                        )
                    )
                );
            }
            if (page.NextPageOffset is null || page.Result.Count == 0)
                break;
            offset = page.NextPageOffset;
        }
        return points;
    }

    public Task DeletePointsAsync(
        VectorCollectionIdentity identity,
        IReadOnlyList<Guid> pointIds,
        CancellationToken cancellationToken = default
    ) =>
        TrackOperationAsync(
            "qdrant",
            "delete",
            pointIds.Count,
            token => DeletePointsCoreAsync(identity, pointIds, token),
            cancellationToken
        );

    private Task DeletePointsCoreAsync(
        VectorCollectionIdentity identity,
        IReadOnlyList<Guid> pointIds,
        CancellationToken cancellationToken
    ) =>
        pointIds.Count == 0
            ? Task.CompletedTask
            : client.DeleteAsync(
                identity.PhysicalCollection,
                pointIds,
                wait: true,
                cancellationToken: cancellationToken
            );

    public Task<string?> GetAliasTargetAsync(
        string alias,
        CancellationToken cancellationToken = default
    ) =>
        TrackOperationAsync(
            "qdrant",
            "collection",
            null,
            token => GetAliasTargetCoreAsync(alias, token),
            cancellationToken
        );

    private async Task<string?> GetAliasTargetCoreAsync(
        string alias,
        CancellationToken cancellationToken = default
    )
    {
        var response = await client.ListAliasesAsync(cancellationToken);
        return response.SingleOrDefault(item => item.AliasName == alias)?.CollectionName;
    }

    public Task SwitchAliasAsync(
        string alias,
        string collection,
        CancellationToken cancellationToken = default
    ) =>
        TrackOperationAsync(
            "qdrant",
            "migration",
            null,
            token => SwitchAliasCoreAsync(alias, collection, token),
            cancellationToken
        );

    private async Task SwitchAliasCoreAsync(
        string alias,
        string collection,
        CancellationToken cancellationToken = default
    )
    {
        var current = await GetAliasTargetCoreAsync(alias, cancellationToken);
        if (current == collection)
            return;
        var operations = new List<AliasOperations>();
        if (current is not null)
            operations.Add(
                new AliasOperations { DeleteAlias = new DeleteAlias { AliasName = alias } }
            );
        operations.Add(
            new AliasOperations
            {
                CreateAlias = new CreateAlias
                {
                    AliasName = alias,
                    CollectionName = collection,
                },
            }
        );
        await client.UpdateAliasesAsync(operations, cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        VectorCollectionIdentity identity,
        string repositoryId,
        ReadOnlyMemory<float> vector,
        VectorSearchFilter filter,
        int limit,
        float? minimumSimilarity = null,
        CancellationToken cancellationToken = default
    ) =>
        TrackOperationAsync(
            "qdrant",
            "query",
            limit,
            token =>
                SearchCoreAsync(
                    identity,
                    repositoryId,
                    vector,
                    filter,
                    limit,
                    minimumSimilarity,
                    token
                ),
            cancellationToken
        );

    private async Task<IReadOnlyList<VectorSearchHit>> SearchCoreAsync(
        VectorCollectionIdentity identity,
        string repositoryId,
        ReadOnlyMemory<float> vector,
        VectorSearchFilter filter,
        int limit,
        float? minimumSimilarity = null,
        CancellationToken cancellationToken = default
    )
    {
        ManagedStoragePathResolver.RequireIdentifier(repositoryId);
        if (!string.Equals(repositoryId, identity.RepositoryId, StringComparison.Ordinal))
            throw new ValidationException("Repository does not match the vector collection.");
        if (
            limit <= 0
            || vector.Length != identity.Fingerprint.Dimension
            || vector.Span.ContainsAnyExceptFinite()
            || (minimumSimilarity.HasValue && !float.IsFinite(minimumSimilarity.Value))
        )
            throw new ValidationException("Invalid vector search request.");
        var conditions = new Filter();
        conditions.Must.Add(MatchKeyword("repositoryId", repositoryId));
        AddKeyword(conditions, "status", filter.Status);
        AddKeyword(conditions, "category", filter.Category);
        AddKeyword(conditions, "tags", filter.Tag);
        AddAnyKeyword(conditions, "status", filter.Statuses);
        AddAnyKeyword(conditions, "category", filter.Categories);
        AddKeyword(conditions, "decisionArea", filter.DecisionArea);
        foreach (var tag in filter.Tags ?? [])
            AddKeyword(conditions, "tags", tag);
        var matches = await client.QueryAsync(
            identity.PhysicalCollection,
            query: vector.ToArray(),
            filter: conditions,
            limit: (ulong)limit,
            payloadSelector: true,
            scoreThreshold: minimumSimilarity,
            cancellationToken: cancellationToken
        );
        return matches
            .Select(point => new VectorSearchHit(
                Guid.Parse(point.Id.Uuid),
                RequiredString(point.Payload, "memoryId"),
                RequiredInteger(point.Payload, "revision"),
                RequiredString(point.Payload, "contentHash"),
                RequiredString(point.Payload, "status"),
                point.Score
            ))
            .ToArray();
    }

    private static Dictionary<string, Value> CreatePayload(
        VectorCollectionIdentity identity,
        MemoryRecord record
    )
    {
        var embedding = record.Embedding!;
        var payload = new Dictionary<string, Value>
        {
            ["memoryId"] = record.Id,
            ["repositoryId"] = record.RepositoryId,
            ["category"] = record.Category,
            ["decisionArea"] = record.DecisionArea ?? string.Empty,
            ["status"] = EnumValue(record.Status),
            ["tags"] = record.Tags.ToArray(),
            ["confidence"] = record.Confidence,
            ["successCount"] = record.SuccessCount,
            ["partialSuccessCount"] = record.PartialSuccessCount,
            ["failureCount"] = record.FailureCount,
            ["contradictionCount"] = record.ContradictionCount,
            ["useCount"] = record.UseCount,
            ["createdAtUtc"] = Timestamp(record.CreatedAtUtc),
            ["updatedAtUtc"] = Timestamp(record.UpdatedAtUtc),
            ["sourceType"] = record.SourceType,
            ["embeddingProvider"] = embedding.Provider,
            ["embeddingModel"] = embedding.Model,
            ["embeddingVersion"] = embedding.EmbeddingVersion,
            ["embeddingFingerprint"] = identity.Fingerprint.Hash,
            ["contentHash"] = record.ContentHash,
            ["revision"] = record.Revision,
        };
        if (record.ValidatedAtUtc.HasValue)
            payload["validatedAtUtc"] = Timestamp(record.ValidatedAtUtc.Value);
        payload["payloadHash"] = ManagedTransactionStore.Hash(
            System.Text.Json.JsonSerializer.Serialize(
                payload
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ToDictionary(pair => pair.Key, pair => ToPolicyValue(pair.Value)),
                new System.Text.Json.JsonSerializerOptions(MemoryJson.Options)
                {
                    WriteIndented = false,
                }
            )
        );
        return payload;
    }

    private static void ValidateRecord(
        VectorCollectionIdentity identity,
        MemoryRecord record,
        ReadOnlySpan<float> vector
    )
    {
        var embedding = record.Embedding;
        if (
            record.Tier != MemoryTier.Long
            || record.RepositoryId != identity.RepositoryId
            || record.Revision <= 0
            || vector.Length != identity.Fingerprint.Dimension
            || vector.ContainsAnyExceptFinite()
            || embedding is null
            || embedding.Dimension != identity.Fingerprint.Dimension
            || embedding.Provider != identity.Fingerprint.Provider
            || embedding.Model != identity.Fingerprint.Model
            || embedding.ModelDigest != identity.Fingerprint.ModelDigest
            || embedding.EmbeddingVersion != identity.Fingerprint.ProjectionVersion
        )
            throw new ValidationException(
                "Record, vector and embedding fingerprint are incompatible.",
                "index_incompatible"
            );
    }

    private static void AddKeyword(Filter filter, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            filter.Must.Add(MatchKeyword(name, value));
    }

    private static void AddAnyKeyword(
        Filter filter,
        string name,
        IReadOnlyList<string>? values
    )
    {
        if (values is { Count: > 0 })
            filter.Must.Add(Match(name, values));
    }

    private static string EnumValue<T>(T value)
        where T : struct, Enum =>
        System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    private static string Timestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string RequiredString(IDictionary<string, Value> payload, string name) =>
        payload.TryGetValue(name, out var value) && value.HasStringValue
            ? value.StringValue
            : throw new InvalidDataException("Qdrant point payload is incomplete.");

    private static long RequiredInteger(IDictionary<string, Value> payload, string name) =>
        payload.TryGetValue(name, out var value) && value.HasIntegerValue
            ? value.IntegerValue
            : throw new InvalidDataException("Qdrant point payload is incomplete.");

    private static object? ToPolicyValue(Value value) =>
        value.KindCase switch
        {
            Value.KindOneofCase.StringValue => value.StringValue,
            Value.KindOneofCase.IntegerValue => value.IntegerValue,
            Value.KindOneofCase.DoubleValue => value.DoubleValue,
            Value.KindOneofCase.BoolValue => value.BoolValue,
            Value.KindOneofCase.ListValue => value.ListValue.Values.Select(ToPolicyValue).ToArray(),
            Value.KindOneofCase.NullValue => null,
            _ => throw new InvalidDataException("Unsupported Qdrant payload value."),
        };

    private static ValidationException RevisionConflict() =>
        new("Vector index revision conflict.", "revision_conflict");
}

internal static class FloatSpanExtensions
{
    public static bool ContainsAnyExceptFinite(this ReadOnlySpan<float> values)
    {
        foreach (var value in values)
            if (!float.IsFinite(value))
                return true;
        return false;
    }
}
