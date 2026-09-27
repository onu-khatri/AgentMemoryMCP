using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;
using AgentSession.MCP.Options;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

internal sealed record MemoryOperationReceipt(string RequestHash, MemoryWriteResult Result);

public sealed partial class CanonicalMemoryService(
    ManagedTransactionStore store,
    LearningCatalog catalog,
    MemoryStoragePaths paths,
    MemoryRequestValidator requests,
    TimeProvider time,
    IOptions<MemoryPolicyOptions> options,
    OperatorGrantVerifier grants,
    ManagedStoragePathResolver resolver,
    OllamaEmbeddingClient embeddings,
    QdrantVectorIndex vectors
)
{
    private static ManagedFile OperationFile(string id) =>
        new(ManagedArea.Learning, null, ["operations", id + ".json"]);

    internal static ManagedFile VectorPendingFile(string memoryId) =>
        new(ManagedArea.Learning, null, ["indexes", "vector-pending", memoryId + ".json"]);

    public async Task<MemoryWriteResult> RememberAsync(
        RememberMemoryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        requests.Validate(request, content: true);
        requests.ValidateOptionalBatch(request.Tags.Count);
        ManagedStoragePathResolver.RequireIdentifier(request.OperationId);
        if (request.RepositoryId is not null && request.RepositoryId != paths.RepositoryId)
            throw new ValidationException("Repository does not match this server.");
        foreach (
            var id in new[]
            {
                request.SessionId,
                request.TaskId,
                request.ParentStepId,
                request.AgentId,
            }
        )
            if (id is not null)
                ManagedStoragePathResolver.RequireIdentifier(id);
        if (
            !Enum.IsDefined(request.Tier)
            || string.IsNullOrWhiteSpace(request.Content)
            || string.IsNullOrWhiteSpace(request.Category)
        )
            throw new ValidationException("Tier, content and category are required.");
        var requestHash = RequestHash("remember", request);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (
                    await PriorAsync(request.OperationId, requestHash, cancellationToken) is
                    { } prior
                )
                    return prior;
                await CleanupAsync(cancellationToken);
                var snapshot = await catalog.ReadAsync(cancellationToken);
                var now = time.GetUtcNow();
                var tier = request.Tier switch
                {
                    RememberTier.Temp => MemoryTier.Temp,
                    RememberTier.Short => MemoryTier.Short,
                    _ => MemoryTier.Long,
                };
                DateTimeOffset? expiry = null;
                if (tier == MemoryTier.Temp)
                {
                    expiry = now.AddHours(options.Value.Temp.MaxAgeHours);
                    if (request.ExpiresAtUtc is { } explicitExpiry)
                    {
                        if (
                            explicitExpiry.Offset != TimeSpan.Zero
                            || explicitExpiry <= now
                            || explicitExpiry > expiry
                        )
                            throw new ValidationException(
                                "Explicit temp expiry must be in UTC, after now and no later than the configured lifetime."
                            );
                        expiry = explicitExpiry;
                    }
                }
                else if (request.ExpiresAtUtc is not null)
                    throw new ValidationException("Only temporary memory has an expiry.");
                var record = new MemoryRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    RepositoryId = paths.RepositoryId,
                    Tier = tier,
                    SessionId = request.SessionId,
                    TaskId = request.TaskId,
                    ParentStepId = request.ParentStepId,
                    AgentId = request.AgentId,
                    Title = request.Title,
                    Content = request.Content,
                    StructuredData = request.StructuredData,
                    Tags = request.Tags,
                    Category = request.Category,
                    DecisionArea = request.DecisionArea,
                    SourceType = request.SourceType,
                    SourceReference = request.SourceReference,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    ExpiresAtUtc = expiry,
                    OperationId = request.OperationId,
                    Protected = request.Protected,
                    ContentHash = MemoryClaimHash.Content(request.Content, request.StructuredData),
                    Status = tier == MemoryTier.Long ? MemoryState.Candidate : MemoryState.Active,
                    ReviewAfterUtc =
                        tier == MemoryTier.Short
                            ? request.ReviewAfterUtc
                                ?? now.AddDays(options.Value.Short.ReviewAfterDays)
                            : null,
                    EmbeddingState =
                        tier == MemoryTier.Long
                            ? EmbeddingState.PendingEmbedding
                            : EmbeddingState.NotRequired,
                    IndexState =
                        tier == MemoryTier.Long
                            ? VectorIndexState.Pending
                            : VectorIndexState.NotRequired,
                };
                MemoryRecordValidator.Validate(record);
                var scope = MemoryClaimHash.Scope(record);
                var duplicate = snapshot.Entries.Values.FirstOrDefault(entry =>
                    entry.ScopeHash == scope && IsOrdinarilyActive(entry) && !IsExpired(entry)
                );
                if (duplicate is not null)
                {
                    var current = await ReadCanonicalAsync(duplicate, cancellationToken);
                    if (request.Protected && !current.Protected)
                        throw new ValidationException(
                            "An existing unprotected claim matches this content; protection requires an explicit revision-controlled policy change.",
                            "operation_conflict"
                        );
                    if (expiry is not null && current.ExpiresAtUtc > expiry)
                        throw new ValidationException(
                            "An existing claim has a later expiry; shorten it through memory_update using its current revision.",
                            "revision_conflict"
                        );
                    var duplicateResult = new MemoryWriteResult(
                        current.Id,
                        current.Revision,
                        current.Tier,
                        current.Status,
                        true
                    );
                    await CommitAsync(
                        request.OperationId,
                        requestHash,
                        duplicateResult,
                        snapshot,
                        [],
                        [],
                        [],
                        request.AgentId,
                        "memory-deduplicated",
                        cancellationToken
                    );
                    return duplicateResult;
                }
                if (
                    tier == MemoryTier.Temp
                    && snapshot.Entries.Values.Count(entry =>
                        entry.Tier == tier
                        && entry.SessionId == request.SessionId
                        && !IsExpired(entry)
                    ) >= options.Value.Temp.MaxEntriesPerSession
                )
                    throw new ValidationException(
                        "Temporary session capacity reached.",
                        "capacity_exceeded"
                    );
                if (
                    tier == MemoryTier.Short
                    && snapshot.Entries.Values.Count(entry =>
                        entry.Tier == tier && IsOrdinarilyActive(entry)
                    ) >= options.Value.Short.MaxActiveRecords
                )
                    throw new ValidationException(
                        "Active short-term capacity reached.",
                        "capacity_exceeded"
                    );
                var result = new MemoryWriteResult(
                    record.Id,
                    record.Revision,
                    record.Tier,
                    record.Status
                );
                await CommitAsync(
                    request.OperationId,
                    requestHash,
                    result,
                    snapshot,
                    [record],
                    [],
                    [
                        new(
                            LearningCatalog.FileFor(record),
                            Serialize(record),
                            null,
                            record.ExpiresAtUtc
                        ),
                    ],
                    request.AgentId,
                    "memory-created",
                    cancellationToken
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

    public async Task<MemoryReadResult> GetAsync(
        GetMemoryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ManagedStoragePathResolver.RequireIdentifier(request.MemoryId);
        await CleanupAsync(cancellationToken);
        var snapshot = await catalog.ReadAsync(cancellationToken);
        if (!snapshot.Entries.TryGetValue(request.MemoryId, out var entry))
            return request.IncludeArchived
                ? await ReadArchivedAsync(request.MemoryId, cancellationToken)
                : new(null, "not_found");
        if (IsExpired(entry))
            return new(null, "expired");
        if (
            !request.IncludeHistory
            && entry.Status is MemoryState.Retired or MemoryState.Superseded or MemoryState.Rejected
        )
            return new(null, "historical");
        var record = await ReadCanonicalAsync(entry, cancellationToken);
        if (IsExpired(LearningCatalog.EntryFor(record)))
            return new(null, "expired");
        return new(record, "available");
    }

    public async Task<MemoryWriteResult> UpdateAsync(
        UpdateMemoryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        requests.Validate(request, content: true);
        requests.ValidateOptionalBatch(request.Tags?.Count ?? 0);
        ManagedStoragePathResolver.RequireIdentifier(request.OperationId);
        ManagedStoragePathResolver.RequireIdentifier(request.AgentId);
        ManagedStoragePathResolver.RequireIdentifier(request.MemoryId);
        var hash = RequestHash("update", request);
        if (await PriorAsync(request.OperationId, hash, cancellationToken) is { } prior)
            return prior;
        var snapshot = await catalog.ReadAsync(cancellationToken);
        if (!snapshot.Entries.TryGetValue(request.MemoryId, out var entry) || IsExpired(entry))
            throw new ValidationException("Memory not found or expired.");
        var record = await ReadCanonicalAsync(entry, cancellationToken);
        if (record.Revision != request.ExpectedRevision)
            throw new ValidationException("Memory revision conflict.", "revision_conflict");
        if (record.Tier != MemoryTier.Temp)
            throw new ValidationException(
                "In-place content updates are supported only for temporary working memory; curate or supersede durable learning explicitly."
            );
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ValidationException("Content is required.");
        if (
            request.ExpiresAtUtc is { } expiry
            && (
                expiry.Offset != TimeSpan.Zero
                || expiry > EffectiveExpiry(entry)
                || expiry <= time.GetUtcNow()
            )
        )
            throw new ValidationException(
                "An update cannot extend temporary expiry or set a nonfuture expiry."
            );
        var previous =
            await store.ReadAsync(new(ManagedArea.Learning, null, entry.Parts), cancellationToken)
            ?? throw new ValidationException(
                "Canonical record disappeared; reread before retrying.",
                "revision_conflict"
            );
        record.Content = request.Content;
        record.StructuredData = request.StructuredData;
        record.Title = request.Title ?? record.Title;
        record.Tags = request.Tags ?? record.Tags;
        record.ExpiresAtUtc = request.ExpiresAtUtc ?? record.ExpiresAtUtc;
        record.AgentId = request.AgentId;
        record.Revision++;
        record.OperationId = request.OperationId;
        record.UpdatedAtUtc = time.GetUtcNow();
        record.ContentHash = MemoryClaimHash.Content(record.Content, record.StructuredData);
        MemoryRecordValidator.Validate(record);
        var result = new MemoryWriteResult(record.Id, record.Revision, record.Tier, record.Status);
        await CommitAsync(
            request.OperationId,
            hash,
            result,
            snapshot,
            [record],
            [],
            [
                new(
                    LearningCatalog.FileFor(record),
                    Serialize(record),
                    ManagedTransactionStore.Hash(previous),
                    record.ExpiresAtUtc
                ),
            ],
            request.AgentId,
            "memory-updated",
            cancellationToken
        );
        return result;
    }

    public async Task<CleanupMemoryResult> CleanupAsync(
        CancellationToken cancellationToken = default
    )
    {
        var snapshot = await catalog.ReadAsync(cancellationToken);
        var expired = snapshot
            .Entries.Values.Where(entry =>
                entry.Tier == MemoryTier.Temp
                && entry.CreatedAtUtc <= time.GetUtcNow()
                && IsExpired(entry)
            )
            .Take(options.Value.Limits.MaxMutationBatch)
            .ToArray();
        var mutations = new List<ManagedMutation>();
        var removed = new List<string>();
        var errors = snapshot.Manifest.InvalidRecordIds.Count;
        foreach (var entry in expired)
        {
            var file = new ManagedFile(ManagedArea.Learning, null, entry.Parts);
            var content = await store.ReadAsync(file, cancellationToken);
            if (content is not null)
            {
                try
                {
                    var record = await ReadCanonicalAsync(entry, cancellationToken);
                    if (
                        record.Tier != MemoryTier.Temp
                        || !IsExpired(LearningCatalog.EntryFor(record))
                    )
                        continue;
                }
                catch (Exception error)
                    when (error is ValidationException or JsonException or InvalidDataException)
                {
                    errors++;
                    continue;
                }
            }
            mutations.Add(
                new(file, null, content is null ? null : ManagedTransactionStore.Hash(content))
            );
            removed.Add(entry.Id);
        }
        if (removed.Count > 0)
        {
            mutations.AddRange(LearningCatalog.Update(snapshot, [], removed));
            await store.CommitAsync(
                "expiry-" + Guid.NewGuid().ToString("N"),
                mutations,
                cancellationToken
            );
        }

        return new(removed.Count, errors);
    }

    public async Task<CleanupMemoryResult> RunMaintenanceAsync(
        CancellationToken cancellationToken = default
    )
    {
        var cleanup = await CleanupAsync(cancellationToken);
        var errors = cleanup.Errors;
        var archived = 0;
        var snapshot = await catalog.ReadAsync(cancellationToken);
        var approved = snapshot.Entries.Values
            .Where(entry => entry.Tier == MemoryTier.Short && IsOrdinarilyActive(entry))
            .OrderBy(entry => entry.CreatedAtUtc)
            .Take(options.Value.Short.MaxActiveRecords)
            .ToArray();
        var processedCandidates = 0;
        foreach (var entry in approved)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MemoryRecord record;
            try
            {
                record = await ReadCanonicalAsync(entry, cancellationToken);
            }
            catch (Exception error)
                when (error is ValidationException or JsonException or InvalidDataException)
            {
                errors++;
                continue;
            }
            if (record.ReviewState != ReviewState.ArchiveCandidate)
                continue;
            if (processedCandidates++ >= options.Value.Limits.MaxMutationBatch)
                break;
            var operationId =
                "maintenance-archive-"
                + ManagedTransactionStore.Hash($"{record.Id}:{record.Revision}")[..24];
            try
            {
                var result = await ArchiveAsync(
                    new(
                        operationId,
                        "maintenance",
                        "Explicitly reviewed archive candidate",
                        [new(record.Id, record.Revision)]
                    ),
                    cancellationToken
                );
                archived += result.ArchivedCount;
            }
            catch (ValidationException)
            {
                // Leave the canonical record intact for operator review or a later retry.
                errors++;
            }
        }
        return new(cleanup.Deleted, errors, archived);
    }

    internal async Task<MemoryRecord> ReadCanonicalAsync(
        CatalogEntry entry,
        CancellationToken cancellationToken
    )
    {
        var content = await store.ReadAsync(
            new(ManagedArea.Learning, null, entry.Parts),
            cancellationToken
        );
        if (content is null)
            throw new ValidationException(
                "Canonical memory is missing; rebuild indexes.",
                "storage_invalid"
            );
        var record =
            JsonSerializer.Deserialize<MemoryRecord>(content, MemoryJson.Options)
            ?? throw new InvalidDataException("Empty canonical record.");
        MemoryRecordValidator.Validate(record);
        if (
            record.RepositoryId != paths.RepositoryId
            || record.Id != entry.Id
            || record.Revision != entry.Revision
            || record.Tier != entry.Tier
            || record.Status != entry.Status
            || !LearningCatalog.FileFor(record).Parts.SequenceEqual(entry.Parts)
            || record.ContentHash != entry.ContentHash
            || MemoryClaimHash.Scope(record) != entry.ScopeHash
            || record.CreatedAtUtc > time.GetUtcNow()
            || record.ContentHash != MemoryClaimHash.Content(record.Content, record.StructuredData)
        )
            throw new ValidationException(
                "Canonical identity, revision, timestamp or content hash is invalid.",
                "storage_invalid"
            );
        requests.Validate(record);
        return record;
    }

    internal bool IsExpired(CatalogEntry entry) =>
        entry.Tier == MemoryTier.Temp
        && (entry.CreatedAtUtc > time.GetUtcNow() || EffectiveExpiry(entry) <= time.GetUtcNow());

    private DateTimeOffset EffectiveExpiry(CatalogEntry entry) =>
        new[]
        {
            entry.ExpiresAtUtc ?? entry.CreatedAtUtc,
            entry.CreatedAtUtc.AddHours(24),
            entry.CreatedAtUtc.AddHours(options.Value.Temp.MaxAgeHours),
        }.Min();

    internal static bool IsOrdinarilyActive(CatalogEntry entry) =>
        entry.Status
            is MemoryState.Active
                or MemoryState.Compacted
                or MemoryState.Candidate
                or MemoryState.Validated;

    internal static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, MemoryJson.Options);

    private static string RequestHash<T>(string action, T value) =>
        ManagedTransactionStore.Hash(action + "\n" + Serialize(value));

    private async Task<MemoryWriteResult?> PriorAsync(
        string operationId,
        string hash,
        CancellationToken cancellationToken
    )
    {
        var json = await store.ReadAsync(OperationFile(operationId), cancellationToken);
        if (json is null)
            return null;
        var receipt = JsonSerializer.Deserialize<MemoryOperationReceipt>(json, MemoryJson.Options)!;
        if (receipt.RequestHash != hash)
            throw new ValidationException(
                "Operation ID was used for different input.",
                "operation_conflict"
            );
        return receipt.Result;
    }

    private async Task CommitAsync(
        string operationId,
        string requestHash,
        MemoryWriteResult result,
        CatalogSnapshot snapshot,
        IReadOnlyList<MemoryRecord> updated,
        IReadOnlyList<string> removed,
        IReadOnlyList<ManagedMutation> files,
        string? actor,
        string eventType,
        CancellationToken cancellationToken,
        string? reason = null,
        MemoryRecord? provenance = null,
        Action? validateCommit = null
    )
    {
        var mutations = files.ToList();
        foreach (var record in updated.Where(record => record.Tier == MemoryTier.Long))
        {
            var pendingFile = VectorPendingFile(record.Id);
            if (mutations.Any(mutation => mutation.File == pendingFile))
                continue;
            var pendingBefore = await store.ReadAsync(pendingFile, cancellationToken);
            mutations.Add(
                new(
                    pendingFile,
                    Serialize(
                        new VectorPendingWork
                        {
                            WorkId = Guid.NewGuid().ToString("N"),
                            RepositoryId = paths.RepositoryId,
                            MemoryId = record.Id,
                            Revision = record.Revision,
                            ContentHash = record.ContentHash,
                            CreatedAtUtc = time.GetUtcNow(),
                            NextAttemptAtUtc = time.GetUtcNow(),
                        }
                    ),
                    pendingBefore is null
                        ? null
                        : ManagedTransactionStore.Hash(pendingBefore)
                )
            );
        }
        mutations.AddRange(
            LearningCatalog.Update(snapshot, updated.Select(LearningCatalog.EntryFor), removed)
        );
        mutations.Add(
            new(
                OperationFile(operationId),
                Serialize(new MemoryOperationReceipt(requestHash, result)),
                null
            )
        );
        var memoryEvent = new MemoryEvent
        {
            EventId = operationId,
            MemoryId = result.MemoryId,
            RepositoryId = paths.RepositoryId,
            AgentId = actor,
            SessionId = (provenance ?? updated.FirstOrDefault())?.SessionId,
            TaskId = (provenance ?? updated.FirstOrDefault())?.TaskId,
            TimestampUtc = time.GetUtcNow(),
            EventType = eventType,
            Metadata = new()
            {
                ["revision"] = result.Revision.ToString(
                    System.Globalization.CultureInfo.InvariantCulture
                ),
            },
        };
        if (reason is not null)
            memoryEvent.Metadata["reason"] = reason;
        mutations.AddRange(await PrepareEventAppendAsync(memoryEvent, cancellationToken));
        await store.CommitAsync(
            "memory-" + ManagedTransactionStore.Hash(operationId),
            mutations,
            cancellationToken,
            () =>
            {
                if (updated.Any(record => IsExpired(LearningCatalog.EntryFor(record))))
                    throw new ValidationException("Temporary memory expired before commit.");
                validateCommit?.Invoke();
            }
        );
    }

    private async Task<IReadOnlyList<ManagedMutation>> PrepareEventAppendAsync(
        MemoryEvent memoryEvent,
        CancellationToken cancellationToken
    )
    {
        var month = memoryEvent.TimestampUtc.ToString(
            "yyyy-MM",
            System.Globalization.CultureInfo.InvariantCulture
        );
        var currentFile = new ManagedFile(
            ManagedArea.Learning,
            null,
            ["events", month, "events.jsonl"]
        );
        var original = await store.ReadAsync(currentFile, cancellationToken);
        var complete = CompleteEventLines(original, out var eventIds);
        if (eventIds.Contains(memoryEvent.EventId))
            return [];

        var line =
            JsonSerializer.Serialize(
                memoryEvent,
                new JsonSerializerOptions(MemoryJson.Options) { WriteIndented = false }
            ) + "\n";
        var expectedHash = original is null ? null : ManagedTransactionStore.Hash(original);
        var combined = complete + line;
        var maxBytes = options.Value.Limits.MaxEventJournalBytes;
        if (System.Text.Encoding.UTF8.GetByteCount(combined) <= maxBytes)
            return [new(currentFile, combined, expectedHash)];
        if (System.Text.Encoding.UTF8.GetByteCount(line) > maxBytes)
            throw new ValidationException(
                "Sanitized event exceeds the configured journal segment limit.",
                "capacity_exceeded"
            );

        var mutations = new List<ManagedMutation>();
        if (complete.Length > 0)
        {
            var segmentHash = ManagedTransactionStore.Hash(complete);
            var segmentFile = new ManagedFile(
                ManagedArea.Learning,
                null,
                ["events", month, $"events-{segmentHash[..16]}.jsonl"]
            );
            var existing = await store.ReadAsync(segmentFile, cancellationToken);
            if (existing is not null && existing != complete)
                throw new ValidationException(
                    "Event journal rotation target is inconsistent.",
                    "storage_invalid"
                );
            if (existing is null)
                mutations.Add(new(segmentFile, complete, null));
        }
        mutations.Add(new(currentFile, line, expectedHash));
        return mutations;
    }

    private static string CompleteEventLines(string? content, out HashSet<string> eventIds)
    {
        eventIds = new(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(content))
            return string.Empty;
        var end = content.LastIndexOf('\n');
        if (end < 0)
            return string.Empty;
        var complete = content[..(end + 1)];
        foreach (var rawLine in complete.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;
            MemoryEvent memoryEvent;
            try
            {
                memoryEvent =
                    JsonSerializer.Deserialize<MemoryEvent>(line, MemoryJson.Options)
                    ?? throw new JsonException("Empty event.");
            }
            catch (JsonException)
            {
                throw new ValidationException(
                    "Event journal contains an invalid complete line.",
                    "storage_invalid"
                );
            }
            if (
                memoryEvent.SchemaVersion != 1
                || string.IsNullOrWhiteSpace(memoryEvent.EventId)
                || !eventIds.Add(memoryEvent.EventId)
            )
                throw new ValidationException(
                    "Event journal contains an invalid or duplicate event ID.",
                    "storage_invalid"
                );
        }
        return complete;
    }
}
