using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Services;

internal sealed record ArchiveManifestEntry(
    string MemoryId,
    long Revision,
    string RecordHash,
    string ContentHash,
    int Line
);

internal sealed record ArchiveManifest(
    int SchemaVersion,
    string ArchiveId,
    string RepositoryId,
    DateTimeOffset CreatedAtUtc,
    string Format,
    long ExpandedBytes,
    long CompressedBytes,
    Dictionary<string, ArchiveManifestEntry> Records
);

internal sealed record ArchiveIndexEntry(
    string[] ManifestParts,
    string ArchiveId,
    DateTimeOffset? ArchivedAtUtc = null
);
internal sealed record ArchiveIndex(int SchemaVersion, Dictionary<string, ArchiveIndexEntry> Records);
internal sealed record ArchiveOperationReceipt(string RequestHash, ArchiveMemoryResult Result);

public sealed partial class CanonicalMemoryService
{
    private static readonly ManagedFile ArchiveIndexFile =
        new(ManagedArea.Learning, null, ["short-term", ".archive-index.json"]);

    public async Task<ArchiveMemoryResult> ArchiveAsync(
        ArchiveMemoryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        requests.Validate(request, content: true);
        requests.ValidateBatch(request.Records.Count);
        ManagedStoragePathResolver.RequireIdentifier(request.OperationId);
        ManagedStoragePathResolver.RequireIdentifier(request.AgentId);
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("Archive rationale is required.");
        if (request.Records.Select(item => item.MemoryId).Distinct(StringComparer.Ordinal).Count()
            != request.Records.Count)
            throw new ValidationException("Archive record IDs must be unique.");
        foreach (var item in request.Records)
        {
            ManagedStoragePathResolver.RequireIdentifier(item.MemoryId);
            if (item.ExpectedRevision < 1)
                throw new ValidationException("Archive revisions must be positive.");
        }
        var requestHash = RequestHash("archive", request);
        if (await PriorArchiveAsync(request.OperationId, requestHash, cancellationToken) is { } prior)
            return prior;

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var snapshot = await catalog.ReadAsync(cancellationToken);
                var records = new List<MemoryRecord>();
                var sourceJson = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var item in request.Records)
                {
                    var record = await ForMutationAsync(
                        snapshot,
                        item.MemoryId,
                        item.ExpectedRevision,
                        cancellationToken
                    );
                    if (record.Tier != MemoryTier.Short)
                        throw new ValidationException("Only short-term memory can be archived.");
                    if (record.Protected)
                        grants.Require(record, OperatorAction.ArchiveProtectedMemory);
                    var json = await store.ReadAsync(
                        LearningCatalog.FileFor(record),
                        cancellationToken
                    ) ?? throw new ValidationException("Archive source disappeared.", "revision_conflict");
                    records.Add(record);
                    sourceJson.Add(record.Id, json);
                }

                var archiveId = "archive-" + ManagedTransactionStore.Hash(request.OperationId)[..24];
                var month = time.GetUtcNow().ToString(
                    "yyyy-MM",
                    System.Globalization.CultureInfo.InvariantCulture
                );
                var archiveParts = new[]
                {
                    "short-term", "archive", month, archiveId + ".jsonl.gz",
                };
                var manifestParts = new[]
                {
                    "short-term", "archive", month, archiveId + ".manifest.json",
                };
                var lines = records.Select(SerializeArchiveRecord).ToArray();
                var expanded = Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n");
                if (expanded.Length > options.Value.Limits.MaxArchiveExpandedBytes)
                    throw new ValidationException(
                        "Archive exceeds the configured expanded byte limit.",
                        "capacity_exceeded"
                    );
                var compressed = Compress(expanded);
                if (compressed.Length > options.Value.Limits.MaxArchiveCompressedBytes)
                    throw new ValidationException(
                        "Archive exceeds the configured compressed byte limit.",
                        "capacity_exceeded"
                    );
                var manifest = new ArchiveManifest(
                    1,
                    archiveId,
                    paths.RepositoryId,
                    time.GetUtcNow(),
                    "jsonl+gzip",
                    expanded.Length,
                    compressed.Length,
                    records.Select((record, index) => new ArchiveManifestEntry(
                            record.Id,
                            record.Revision,
                            ManagedTransactionStore.Hash(lines[index]),
                            record.ContentHash,
                            index
                        ))
                        .ToDictionary(entry => entry.MemoryId, StringComparer.Ordinal)
                );
                await store.CommitAsync(
                    "archive-stage-" + ManagedTransactionStore.Hash(request.OperationId),
                    [
                        new(
                            new(ManagedArea.Learning, null, archiveParts),
                            null,
                            null,
                            null,
                            Convert.ToBase64String(compressed)
                        ),
                        new(
                            new(ManagedArea.Learning, null, manifestParts),
                            Serialize(manifest),
                            null
                        ),
                    ],
                    cancellationToken
                );
                await VerifyArchiveAsync(archiveParts, manifest, cancellationToken);

                var indexJson = await store.ReadAsync(ArchiveIndexFile, cancellationToken);
                var archiveIndex = indexJson is null
                    ? new ArchiveIndex(1, new(StringComparer.Ordinal))
                    : JsonSerializer.Deserialize<ArchiveIndex>(indexJson, MemoryJson.Options)
                        ?? throw new ValidationException("Archive index is invalid.", "storage_invalid");
                if (archiveIndex.SchemaVersion != 1)
                    throw new ValidationException("Archive index version is unsupported.", "storage_invalid");
                foreach (var record in records)
                    archiveIndex.Records[record.Id] = new(
                        manifestParts,
                        archiveId,
                        manifest.CreatedAtUtc
                    );

                var result = new ArchiveMemoryResult(
                    archiveId,
                    records.Select(record => record.Id).ToArray(),
                    records.Count
                );
                var finalMutations = records
                    .Select(record => new ManagedMutation(
                        LearningCatalog.FileFor(record),
                        null,
                        ManagedTransactionStore.Hash(sourceJson[record.Id])
                    ))
                    .ToList();
                finalMutations.AddRange(
                    LearningCatalog.Update(snapshot, [], records.Select(record => record.Id))
                );
                finalMutations.Add(
                    new(
                        ArchiveIndexFile,
                        Serialize(archiveIndex),
                        indexJson is null ? null : ManagedTransactionStore.Hash(indexJson)
                    )
                );
                finalMutations.Add(
                    new(
                        OperationFile(request.OperationId),
                        Serialize(new ArchiveOperationReceipt(requestHash, result)),
                        null
                    )
                );
                var memoryEvent = new MemoryEvent
                {
                    EventId = request.OperationId,
                    RepositoryId = paths.RepositoryId,
                    AgentId = request.AgentId,
                    TimestampUtc = time.GetUtcNow(),
                    EventType = "memory-archived",
                    Metadata = new()
                    {
                        ["archiveId"] = archiveId,
                        ["count"] = records.Count.ToString(
                            System.Globalization.CultureInfo.InvariantCulture
                        ),
                        ["reason"] = request.Reason,
                    },
                };
                finalMutations.AddRange(
                    await PrepareEventAppendAsync(memoryEvent, cancellationToken)
                );
                await store.CommitAsync(
                    "memory-" + ManagedTransactionStore.Hash(request.OperationId),
                    finalMutations,
                    cancellationToken,
                    () =>
                    {
                        foreach (var record in records.Where(record => record.Protected))
                            grants.Require(record, OperatorAction.ArchiveProtectedMemory);
                    }
                );
                return result;
            }
            catch (ValidationException error)
                when (error.Code == "revision_conflict" && attempt < 3)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private async Task<ArchiveMemoryResult?> PriorArchiveAsync(
        string operationId,
        string requestHash,
        CancellationToken cancellationToken
    )
    {
        var json = await store.ReadAsync(OperationFile(operationId), cancellationToken);
        if (json is null)
            return null;
        ArchiveOperationReceipt? receipt;
        try
        {
            receipt = JsonSerializer.Deserialize<ArchiveOperationReceipt>(json, MemoryJson.Options);
        }
        catch (JsonException)
        {
            throw new ValidationException(
                "Operation ID belongs to another memory action.",
                "operation_conflict"
            );
        }
        if (receipt is null || receipt.RequestHash != requestHash)
            throw new ValidationException(
                "Operation ID was used for different input.",
                "operation_conflict"
            );
        return receipt.Result;
    }

    private async Task<MemoryReadResult> ReadArchivedAsync(
        string memoryId,
        CancellationToken cancellationToken
    )
    {
        var indexJson = await store.ReadAsync(ArchiveIndexFile, cancellationToken);
        if (indexJson is null)
            return new(null, "not_found");
        var index = JsonSerializer.Deserialize<ArchiveIndex>(indexJson, MemoryJson.Options)
            ?? throw new ValidationException("Archive index is invalid.", "storage_invalid");
        if (!index.Records.TryGetValue(memoryId, out var entry))
            return new(null, "not_found");
        var manifestJson = await store.ReadAsync(
            new(ManagedArea.Learning, null, entry.ManifestParts),
            cancellationToken
        ) ?? throw new ValidationException("Archive manifest is missing.", "storage_invalid");
        var manifest = JsonSerializer.Deserialize<ArchiveManifest>(manifestJson, MemoryJson.Options)
            ?? throw new ValidationException("Archive manifest is invalid.", "storage_invalid");
        var archiveParts = entry.ManifestParts.ToArray();
        archiveParts[^1] = entry.ArchiveId + ".jsonl.gz";
        var records = await VerifyArchiveAsync(archiveParts, manifest, cancellationToken);
        return manifest.Records.TryGetValue(memoryId, out var item)
            ? new(records[item.Line], "archived")
            : new(null, "not_found");
    }

    private async Task<IReadOnlyList<MemoryRecord>> VerifyArchiveAsync(
        string[] archiveParts,
        ArchiveManifest manifest,
        CancellationToken cancellationToken
    )
    {
        if (
            manifest.SchemaVersion != 1
            || manifest.RepositoryId != paths.RepositoryId
            || manifest.Format != "jsonl+gzip"
        )
            throw new ValidationException("Archive manifest is invalid.", "storage_invalid");
        var archivePath = resolver.LearningFile(paths.RepositoryId, archiveParts);
        if (!File.Exists(archivePath))
            throw new ValidationException("Archive file is missing.", "storage_invalid");
        var compressed = await File.ReadAllBytesAsync(archivePath, cancellationToken);
        if (
            compressed.Length != manifest.CompressedBytes
            || compressed.Length > options.Value.Limits.MaxArchiveCompressedBytes
        )
            throw new ValidationException("Archive compressed size is invalid.", "storage_invalid");
        byte[] expanded;
        try
        {
            await using var source = new MemoryStream(compressed, writable: false);
            await using var gzip = new GZipStream(source, CompressionMode.Decompress);
            await using var target = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var read = await gzip.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                    break;
                if (target.Length + read > options.Value.Limits.MaxArchiveExpandedBytes)
                    throw new ValidationException(
                        "Archive exceeds the configured expansion limit.",
                        "capacity_exceeded"
                    );
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            expanded = target.ToArray();
        }
        catch (InvalidDataException)
        {
            throw new ValidationException("Archive gzip data is corrupt.", "storage_invalid");
        }
        if (expanded.Length != manifest.ExpandedBytes)
            throw new ValidationException("Archive expanded size is invalid.", "storage_invalid");
        var text = new UTF8Encoding(false, true).GetString(expanded);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != manifest.Records.Count)
            throw new ValidationException("Archive record count is invalid.", "storage_invalid");
        var records = new List<MemoryRecord>(lines.Length);
        for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
        {
            var record = JsonSerializer.Deserialize<MemoryRecord>(lines[lineNumber], MemoryJson.Options)
                ?? throw new ValidationException("Archive record is invalid.", "storage_invalid");
            MemoryRecordValidator.Validate(record);
            requests.Validate(record);
            if (
                !manifest.Records.TryGetValue(record.Id, out var entry)
                || entry.Line != lineNumber
                || entry.Revision != record.Revision
                || entry.ContentHash != record.ContentHash
                || entry.RecordHash != ManagedTransactionStore.Hash(lines[lineNumber])
            )
                throw new ValidationException("Archive record hash is invalid.", "storage_invalid");
            records.Add(record);
        }
        return records;
    }

    private static byte[] Compress(byte[] content)
    {
        using var target = new MemoryStream();
        using (var gzip = new GZipStream(target, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(content);
        return target.ToArray();
    }

    private static string SerializeArchiveRecord(MemoryRecord record) =>
        JsonSerializer.Serialize(
            record,
            new JsonSerializerOptions(MemoryJson.Options) { WriteIndented = false }
        );
}
