using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Options;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

public enum ManagedArea
{
    Session,
    Learning,
}

public sealed record ManagedFile(ManagedArea Area, string? SessionId, string[] Parts);

public sealed record ManagedMutation(
    ManagedFile File,
    string? Content,
    string? ExpectedHash,
    DateTimeOffset? ExpiresAtUtc = null,
    string? BinaryContentBase64 = null
);

public sealed record ManagedCommit(
    string OperationId,
    string RequestHash,
    List<ManagedMutation> Mutations
);

public sealed record ManagedReceipt(string OperationId, string RequestHash);

public sealed record ManagedDocument(ManagedFile File, string Content);

/// <summary>
/// Roll-forward multi-file commits. All managed readers must acquire the repository lock and
/// recover intents before reading. An intent is the durable commit decision, not a user audit event.
/// </summary>
public sealed class ManagedTransactionStore(
    MemoryStoragePaths storage,
    ManagedStoragePathResolver paths,
    RepositoryMutationLock gate,
    IMemoryContentPolicy policy,
    IOptions<MemoryPolicyOptions> options,
    TimeProvider time
)
{
    private readonly SystemFileSystem _files = new();

    // Fault injection at durable boundaries; production leaves this unset.
    internal Action<int>? AfterMutation { get; set; }
    internal Action<string>? AfterDurableBoundary { get; set; }

    public static string Hash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static string Hash(byte[] content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));

    public async Task<int> GetRecoveryBacklogAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var lease = await gate.AcquireAsync(
            TimeSpan.FromSeconds(30),
            cancellationToken
        );
        var folder = Path.GetDirectoryName(OperationFile("probe", "intent"))!;
        if (!Directory.Exists(folder))
            return 0;
        return Directory.EnumerateFiles(folder, "*.intent.json").Take(100_001).Count();
    }

    public async Task<string?> ReadAsync(
        ManagedFile file,
        CancellationToken cancellationToken = default
    ) => (await ReadManyAsync([file], cancellationToken))[0];

    public async Task<IReadOnlyList<string>> ListSessionIdsAsync(
        int count,
        string? after,
        CancellationToken cancellationToken = default
    )
    {
        if (count < 1 || count > options.Value.Limits.MaxRecallResults + 1)
            throw new ValidationException("Invalid session listing limit.");
        if (after is not null)
            ManagedStoragePathResolver.RequireIdentifier(after);
        await using var lease = await gate.AcquireAsync(
            TimeSpan.FromSeconds(30),
            cancellationToken
        );
        await RecoverAsync();
        paths.SessionFile(storage.RepositoryId, "probe", "coordination", "metadata.json");
        if (!Directory.Exists(storage.SessionsRoot))
            return [];
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var directory in Directory.EnumerateDirectories(storage.SessionsRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = Path.GetFileName(directory);
            if (
                !NameSanitizer.IsSafePathSegment(id)
                || (after is not null && string.CompareOrdinal(id, after) <= 0)
            )
                continue;
            var metadata = paths.SessionFile(
                storage.RepositoryId,
                id,
                "coordination",
                "metadata.json"
            );
            if (!File.Exists(metadata))
                continue;
            ids.Add(id);
            if (ids.Count > count)
                ids.Remove(ids.Max!);
        }
        return ids.ToArray();
    }

    internal async Task<IReadOnlyList<ManagedDocument>> ScanLearningRecordsAsync(
        CancellationToken cancellationToken
    )
    {
        string[][] roots =
        [
            ["temp", "sessions"],
            ["short-term", "active"],
            ["short-term", "compacted"],
            ["long-term", "candidates"],
            ["long-term", "records"],
            ["long-term", "superseded"],
            ["long-term", "retired"],
        ];
        await using var lease = await gate.AcquireAsync(
            TimeSpan.FromSeconds(30),
            cancellationToken
        );
        await RecoverAsync();
        var records = new List<ManagedDocument>();
        foreach (var root in roots)
        {
            var folders = new Stack<string[]>();
            folders.Push(root);
            while (folders.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parts = folders.Pop();
                var folder = Path.GetDirectoryName(
                    paths.LearningFile(storage.RepositoryId, [.. parts, "probe.json"])
                )!;
                if (!Directory.Exists(folder))
                    continue;
                foreach (var child in Directory.EnumerateDirectories(folder))
                {
                    var childParts = parts.Append(Path.GetFileName(child)).ToArray();
                    paths.LearningFile(storage.RepositoryId, [.. childParts, "probe.json"]);
                    if (childParts.Length > 8)
                        throw new ValidationException(
                            "Canonical folder nesting exceeds supported layout."
                        );
                    folders.Push(childParts);
                }
                foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
                {
                    var recordFile = new ManagedFile(
                        ManagedArea.Learning,
                        null,
                        [.. parts, Path.GetFileName(file)]
                    );
                    var resolved = Resolve(recordFile);
                    if (new FileInfo(resolved).Length > options.Value.Limits.MaxRecordBytes)
                        throw new ValidationException(
                            "Canonical file exceeds the configured byte limit.",
                            "capacity_exceeded"
                        );
                    records.Add(
                        new(recordFile, await File.ReadAllTextAsync(resolved, cancellationToken))
                    );
                    if (records.Count > 100_000)
                        throw new ValidationException(
                            "Canonical rebuild exceeds its 100000-record safety bound.",
                            "capacity_exceeded"
                        );
                }
            }
        }
        return records;
    }

    internal async Task<IReadOnlyList<ManagedDocument>> ScanLearningDirectoryAsync(
        string[] directoryParts,
        int maxResults,
        CancellationToken cancellationToken
    )
    {
        if (maxResults < 1 || maxResults > options.Value.Limits.MaxMutationBatch)
            throw new ValidationException("Invalid managed directory scan bound.");
        await using var lease = await gate.AcquireAsync(
            TimeSpan.FromSeconds(30),
            cancellationToken
        );
        await RecoverAsync();
        var probe = paths.LearningFile(
            storage.RepositoryId,
            [.. directoryParts, "probe.json"]
        );
        var directory = Path.GetDirectoryName(probe)!;
        if (!Directory.Exists(directory))
            return [];
        var documents = new List<ManagedDocument>();
        foreach (
            var path in Directory
                .EnumerateFiles(directory, "*.json")
                .Order(StringComparer.Ordinal)
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = new ManagedFile(
                ManagedArea.Learning,
                null,
                [.. directoryParts, Path.GetFileName(path)]
            );
            var resolved = Resolve(file);
            if (new FileInfo(resolved).Length > options.Value.Limits.MaxRecordBytes)
                throw new ValidationException(
                    "Managed file exceeds the configured byte limit.",
                    "capacity_exceeded"
                );
            documents.Add(new(file, await File.ReadAllTextAsync(resolved, cancellationToken)));
            if (documents.Count >= maxResults)
                break;
        }
        return documents;
    }

    public async Task<IReadOnlyList<string?>> ReadManyAsync(
        IReadOnlyList<ManagedFile> files,
        CancellationToken cancellationToken = default
    )
    {
        await using var lease = await gate.AcquireAsync(
            TimeSpan.FromSeconds(30),
            cancellationToken
        );
        await RecoverAsync();
        var result = new List<string?>();
        foreach (var file in files)
        {
            var path = Resolve(file);
            if (
                File.Exists(path)
                && new FileInfo(path).Length > options.Value.Limits.MaxRecordBytes
            )
                throw new ValidationException(
                    "Managed file exceeds the configured byte limit.",
                    "capacity_exceeded"
                );
            result.Add(
                File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null
            );
        }
        return result;
    }

    public async Task<ManagedReceipt> CommitAsync(
        string operationId,
        IReadOnlyList<ManagedMutation> mutations,
        CancellationToken cancellationToken = default,
        Action? validateCommit = null
    )
    {
        ManagedStoragePathResolver.RequireIdentifier(operationId);
        // The public limit counts logical records. Internal index, journal, receipt and metadata
        // files must not reduce that allowance; keep a separate bounded transaction overhead.
        if (
            mutations.Count == 0
            || mutations.Count > Math.Max(512, (long)options.Value.Limits.MaxMutationBatch * 8 + 16)
        )
            throw new ValidationException(
                "Mutation batch is empty or exceeds the configured limit."
            );
        var normalized = mutations.ToList();
        policy.Validate(normalized);
        foreach (var mutation in normalized)
        {
            Resolve(mutation.File);
            if (mutation.Content is not null && mutation.BinaryContentBase64 is not null)
                throw new ValidationException("A mutation cannot contain both text and binary data.");
            if (
                mutation.Content is not null
                && Encoding.UTF8.GetByteCount(mutation.Content)
                    > options.Value.Limits.MaxRecordBytes
            )
                throw new ValidationException("Record exceeds the configured byte limit.");
            if (mutation.BinaryContentBase64 is not null)
            {
                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(mutation.BinaryContentBase64);
                }
                catch (FormatException)
                {
                    throw new ValidationException("Binary mutation is not valid base64.");
                }
                if (bytes.Length > options.Value.Limits.MaxArchiveCompressedBytes)
                    throw new ValidationException(
                        "Archive exceeds the configured compressed byte limit.",
                        "capacity_exceeded"
                    );
            }
        }
        var requestHash = Hash(JsonSerializer.Serialize(normalized, MemoryJson.Options));
        await using var lease = await gate.AcquireAsync(
            TimeSpan.FromSeconds(30),
            cancellationToken
        );
        await RecoverAsync();
        var receiptPath = OperationFile(operationId, "receipt");
        if (File.Exists(receiptPath))
        {
            var existing = JsonSerializer.Deserialize<ManagedReceipt>(
                await File.ReadAllTextAsync(receiptPath, cancellationToken),
                MemoryJson.Options
            )!;
            if (existing.RequestHash != requestHash)
                throw new ValidationException(
                    "Idempotency key was already used for different content.",
                    "operation_conflict"
                );
            return existing;
        }
        var unique = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal
        );
        foreach (var mutation in normalized)
        {
            var path = Resolve(mutation.File);
            if (!unique.Add(path))
                throw new ValidationException("A batch cannot mutate the same file twice.");
            var actual = File.Exists(path)
                ? Hash(await File.ReadAllBytesAsync(path, cancellationToken))
                : null;
            if (!string.Equals(actual, mutation.ExpectedHash, StringComparison.Ordinal))
                throw new ValidationException(
                    "Record revision conflict; reread before retrying.",
                    "revision_conflict"
                );
        }
        validateCommit?.Invoke();
        var intent = new ManagedCommit(operationId, requestHash, normalized);
        // Cancellation before this durable write aborts. Afterwards recovery must finish the commit.
        await _files.WriteAllTextAtomicAsync(
            OperationFile(operationId, "intent"),
            JsonSerializer.Serialize(intent, MemoryJson.Options),
            cancellationToken
        );
        AfterDurableBoundary?.Invoke("intent");
        return await ApplyAsync(intent);
    }

    private async Task RecoverAsync()
    {
        var folder = Path.GetDirectoryName(OperationFile("probe", "intent"))!;
        if (!Directory.Exists(folder))
            return;
        foreach (
            var file in Directory
                .EnumerateFiles(folder, "*.intent.json")
                .Order(StringComparer.Ordinal)
        )
        {
            var operationId = Path.GetFileName(file)[..^".intent.json".Length];
            ManagedStoragePathResolver.RequireIdentifier(operationId);
            var verifiedPath = OperationFile(operationId, "intent");
            var intent =
                JsonSerializer.Deserialize<ManagedCommit>(
                    await File.ReadAllTextAsync(verifiedPath),
                    MemoryJson.Options
                ) ?? throw new InvalidDataException("Invalid pending operation.");
            if (
                intent.OperationId != operationId
                || Hash(JsonSerializer.Serialize(intent.Mutations, MemoryJson.Options))
                    != intent.RequestHash
            )
                throw new InvalidDataException("Pending operation integrity check failed.");
            await ApplyAsync(intent);
        }
    }

    private async Task<ManagedReceipt> ApplyAsync(ManagedCommit intent)
    {
        policy.Validate(intent.Mutations);
        for (var index = 0; index < intent.Mutations.Count; index++)
        {
            var mutation = intent.Mutations[index];
            var path = Resolve(mutation.File);
            if (
                (mutation.Content is null && mutation.BinaryContentBase64 is null)
                || mutation.ExpiresAtUtc <= time.GetUtcNow()
            )
                File.Delete(path);
            else if (mutation.BinaryContentBase64 is not null)
                await _files.WriteAllBytesAtomicAsync(
                    path,
                    Convert.FromBase64String(mutation.BinaryContentBase64),
                    CancellationToken.None
                );
            else
                await _files.WriteAllTextAtomicAsync(
                    path,
                    mutation.Content!,
                    CancellationToken.None
                );
            AfterMutation?.Invoke(index);
            AfterDurableBoundary?.Invoke("mutation-" + index);
        }
        var receipt = new ManagedReceipt(intent.OperationId, intent.RequestHash);
        await _files.WriteAllTextAtomicAsync(
            OperationFile(intent.OperationId, "receipt"),
            JsonSerializer.Serialize(receipt, MemoryJson.Options),
            CancellationToken.None
        );
        AfterDurableBoundary?.Invoke("receipt");
        File.Delete(OperationFile(intent.OperationId, "intent"));
        AfterDurableBoundary?.Invoke("complete");
        return receipt;
    }

    private string OperationFile(string id, string kind) =>
        paths.LearningFile(storage.RepositoryId, ".operations", id + "." + kind + ".json");

    private string Resolve(ManagedFile file)
    {
        if (
            file.Parts.Length == 0
            || file.Parts.Any(part => part is ".operations" or ".locks" or "grants")
        )
            throw new ValidationException("Reserved or empty managed path.");
        return file.Area switch
        {
            ManagedArea.Session when file.SessionId is not null => paths.SessionFile(
                storage.RepositoryId,
                file.SessionId,
                file.Parts
            ),
            ManagedArea.Learning when file.SessionId is null => paths.LearningFile(
                storage.RepositoryId,
                file.Parts
            ),
            _ => throw new ValidationException("Invalid managed storage area."),
        };
    }
}
