using System.Text.Json;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Services;

internal sealed record CatalogBucket(string Hash, int Count);

internal sealed class CatalogManifest
{
    public int SchemaVersion { get; set; } = 1;
    public long Generation { get; set; }
    public Dictionary<string, CatalogBucket> Buckets { get; set; } = [];
    public List<string> InvalidRecordIds { get; set; } = [];
}

internal sealed record CatalogEntry(
    string Id,
    MemoryTier Tier,
    MemoryState Status,
    string[] Parts,
    long Revision,
    string ContentHash,
    string ScopeHash,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    string? SessionId
);

internal sealed record CatalogSnapshot(
    string? ManifestJson,
    CatalogManifest Manifest,
    Dictionary<string, string?> BucketJson,
    Dictionary<string, CatalogEntry> Entries
);

/// <summary>Derived sharded metadata; canonical JSON remains authoritative. Every writer compares the catalog generation.</summary>
public sealed class LearningCatalog(
    ManagedTransactionStore store,
    MemoryStoragePaths paths,
    TimeProvider time
)
{
    internal static readonly ManagedFile ManifestFile = new(
        ManagedArea.Learning,
        null,
        [".index.json"]
    );

    private static ManagedFile BucketFile(string bucket) =>
        new(ManagedArea.Learning, null, ["indexes", bucket + ".json"]);

    private static string BucketFor(string id) => ManagedTransactionStore.Hash(id)[..2];

    internal async Task<CatalogSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var json = await store.ReadAsync(ManifestFile, cancellationToken);
            if (json is null)
            {
                await RebuildAsync(cancellationToken);
                continue;
            }
            try
            {
                var manifest = JsonSerializer.Deserialize<CatalogManifest>(
                    json,
                    MemoryJson.Options
                )!;
                if (manifest.SchemaVersion != 1)
                    throw new InvalidDataException("Unsupported catalog version.");
                var buckets = manifest.Buckets.Keys.Order(StringComparer.Ordinal).ToArray();
                var documents = await store.ReadManyAsync(
                    [ManifestFile, .. buckets.Select(BucketFile)],
                    cancellationToken
                );
                if (documents[0] != json)
                    continue;
                var entries = new Dictionary<string, CatalogEntry>();
                var bucketJson = new Dictionary<string, string?>();
                for (var index = 0; index < buckets.Length; index++)
                {
                    var content = documents[index + 1];
                    if (
                        content is null
                        || ManagedTransactionStore.Hash(content)
                            != manifest.Buckets[buckets[index]].Hash
                    )
                        throw new JsonException("Invalid catalog shard.");
                    var values = JsonSerializer.Deserialize<List<CatalogEntry>>(
                        content,
                        MemoryJson.Options
                    )!;
                    if (values.Count != manifest.Buckets[buckets[index]].Count)
                        throw new JsonException("Invalid catalog count.");
                    foreach (var entry in values)
                    {
                        ManagedStoragePathResolver.RequireIdentifier(entry.Id);
                        if (
                            BucketFor(entry.Id) != buckets[index]
                            || !entries.TryAdd(entry.Id, entry)
                        )
                            throw new JsonException("Invalid catalog ID.");
                    }
                    bucketJson.Add(buckets[index], content);
                }
                return new(json, manifest, bucketJson, entries);
            }
            catch (JsonException)
            {
                await RebuildAsync(cancellationToken);
            }
        }
        throw new ValidationException("Catalog changed repeatedly; retry.", "revision_conflict");
    }

    public async Task<int> RebuildAsync(CancellationToken cancellationToken = default)
    {
        var previous = await store.ReadAsync(ManifestFile, cancellationToken);
        var documents = await store.ScanLearningRecordsAsync(cancellationToken);
        var entries = new Dictionary<string, CatalogEntry>();
        var invalid = new HashSet<string>();
        foreach (var document in documents)
        {
            var id = Path.GetFileNameWithoutExtension(document.File.Parts[^1]);
            try
            {
                var record =
                    JsonSerializer.Deserialize<MemoryRecord>(document.Content, MemoryJson.Options)
                    ?? throw new JsonException("Empty record.");
                MemoryRecordValidator.Validate(record);
                if (
                    record.Id != id
                    || record.RepositoryId != paths.RepositoryId
                    || record.CreatedAtUtc > time.GetUtcNow()
                    || record.ContentHash
                        != MemoryClaimHash.Content(record.Content, record.StructuredData)
                    || !FileFor(record).Parts.SequenceEqual(document.File.Parts)
                    || !entries.TryAdd(id, EntryFor(record))
                )
                    throw new ValidationException("Canonical identity or hash mismatch.");
            }
            catch (Exception error) when (error is JsonException or ValidationException)
            {
                invalid.Add(id);
                entries.Remove(id);
            }
        }
        foreach (var id in invalid)
            entries.Remove(id);
        long generation = 0;
        if (previous is not null)
        {
            try
            {
                generation =
                    JsonSerializer
                        .Deserialize<CatalogManifest>(previous, MemoryJson.Options)
                        ?.Generation
                    ?? 0;
            }
            catch (JsonException)
            { /* malformed derived data is rebuilt from canonical files */
            }
        }
        var manifest = new CatalogManifest
        {
            Generation = checked(generation + 1),
            InvalidRecordIds = invalid.Order(StringComparer.Ordinal).Take(1000).ToList(),
        };
        var mutations = new List<ManagedMutation>();
        foreach (var group in entries.Values.GroupBy(entry => BucketFor(entry.Id)))
        {
            var file = BucketFile(group.Key);
            var old = await store.ReadAsync(file, cancellationToken);
            var content = JsonSerializer.Serialize(
                group.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToList(),
                MemoryJson.Options
            );
            mutations.Add(new(file, content, HashOrNull(old)));
            manifest.Buckets.Add(
                group.Key,
                new(ManagedTransactionStore.Hash(content), group.Count())
            );
        }
        mutations.Add(
            new(
                ManifestFile,
                JsonSerializer.Serialize(manifest, MemoryJson.Options),
                HashOrNull(previous)
            )
        );
        await store.CommitAsync(
            "rebuild-" + Guid.NewGuid().ToString("N"),
            mutations,
            cancellationToken
        );
        return invalid.Count;
    }

    internal static CatalogEntry EntryFor(MemoryRecord record) =>
        new(
            record.Id,
            record.Tier,
            record.Status,
            FileFor(record).Parts,
            record.Revision,
            record.ContentHash,
            MemoryClaimHash.Scope(record),
            record.CreatedAtUtc,
            record.ExpiresAtUtc,
            record.SessionId
        );

    internal static ManagedFile FileFor(MemoryRecord record)
    {
        var month = record.CreatedAtUtc.ToString(
            "yyyy-MM",
            System.Globalization.CultureInfo.InvariantCulture
        );
        string[] parts = record.Tier switch
        {
            MemoryTier.Temp =>
            [
                "temp",
                "sessions",
                record.SessionId ?? "unscoped",
                record.Id + ".json",
            ],
            MemoryTier.Short =>
            [
                "short-term",
                record.Status == MemoryState.Compacted ? "compacted" : "active",
                month,
                record.Id + ".json",
            ],
            MemoryTier.Long =>
            [
                "long-term",
                record.Status switch
                {
                    MemoryState.Candidate => "candidates",
                    MemoryState.Validated => "records",
                    MemoryState.Superseded => "superseded",
                    MemoryState.Retired or MemoryState.Rejected => "retired",
                    _ => throw new ValidationException("Invalid lifecycle."),
                },
                record.Id + ".json",
            ],
            _ => throw new ValidationException("Invalid memory tier."),
        };
        return new(ManagedArea.Learning, null, parts);
    }

    internal static IReadOnlyList<ManagedMutation> Update(
        CatalogSnapshot snapshot,
        IEnumerable<CatalogEntry> updates,
        IEnumerable<string>? remove = null
    )
    {
        var changed = new HashSet<string>();
        foreach (var id in remove ?? [])
        {
            snapshot.Entries.Remove(id);
            changed.Add(BucketFor(id));
        }
        foreach (var entry in updates)
        {
            snapshot.Entries[entry.Id] = entry;
            changed.Add(BucketFor(entry.Id));
        }
        var mutations = new List<ManagedMutation>();
        foreach (var bucket in changed)
        {
            var entries = snapshot
                .Entries.Values.Where(entry => BucketFor(entry.Id) == bucket)
                .OrderBy(entry => entry.Id, StringComparer.Ordinal)
                .ToArray();
            var content = JsonSerializer.Serialize(entries, MemoryJson.Options);
            snapshot.Manifest.Buckets[bucket] = new(
                ManagedTransactionStore.Hash(content),
                entries.Length
            );
            mutations.Add(
                new(
                    BucketFile(bucket),
                    content,
                    HashOrNull(snapshot.BucketJson.GetValueOrDefault(bucket))
                )
            );
        }
        snapshot.Manifest.Generation++;
        mutations.Add(
            new(
                ManifestFile,
                JsonSerializer.Serialize(snapshot.Manifest, MemoryJson.Options),
                HashOrNull(snapshot.ManifestJson)
            )
        );
        return mutations;
    }

    private static string? HashOrNull(string? content) =>
        content is null ? null : ManagedTransactionStore.Hash(content);
}
