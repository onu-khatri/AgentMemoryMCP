using System.Text;
using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Services;

public sealed partial class CanonicalMemoryService
{
    public async Task<PreparedCompactionResult> PrepareCompactionAsync(
        PrepareCompactionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ValidateCompactionSources(request.OperationId, request.AgentId, request.Sources);
        requests.ValidateBatch(request.Sources.Count);
        requests.Validate(request, content: true);
        var snapshot = await catalog.ReadAsync(cancellationToken);
        var sources = await ReadCompactionSourcesAsync(snapshot, request.Sources, cancellationToken);
        return new(CompactionToken(sources), sources);
    }

    public async Task<CompactionResult> CompactAsync(
        CompactMemoryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var prepared = await PrepareCompactionAsync(
            new(request.OperationId + "-prepare", request.AgentId, request.Sources),
            cancellationToken
        );
        return await CommitCompactionAsync(
            new(
                request.OperationId,
                prepared.PreparationToken,
                request.AgentId,
                request.Reason,
                request.Title,
                request.Summary,
                request.Facts,
                request.OpenQuestions,
                request.Contradictions,
                request.SuccessfulPatterns,
                request.FailedPatterns,
                request.Evidence,
                request.Sources,
                request.SourceCoverage,
                request.Tags,
                request.DecisionArea,
                request.SessionId,
                request.TaskId,
                request.ArchiveOriginals,
                request.Kind
            ),
            cancellationToken
        );
    }

    public async Task<CompactionResult> CommitCompactionAsync(
        CommitCompactionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ValidateCompactionSources(request.OperationId, request.AgentId, request.Sources);
        requests.ValidateBatch(request.Sources.Count);
        requests.ValidateBatch(request.SourceCoverage.Count);
        foreach (var count in new[]
        {
            request.Facts.Count,
            request.OpenQuestions.Count,
            request.Contradictions.Count,
            request.SuccessfulPatterns.Count,
            request.FailedPatterns.Count,
            request.Evidence.Count,
            request.Tags.Count,
        })
            requests.ValidateOptionalBatch(count);
        requests.Validate(request, content: true);
        if (
            string.IsNullOrWhiteSpace(request.PreparationToken)
            || string.IsNullOrWhiteSpace(request.Title)
            || string.IsNullOrWhiteSpace(request.Summary)
            || string.IsNullOrWhiteSpace(request.Reason)
            || !Enum.IsDefined(request.Kind)
        )
            throw new ValidationException(
                "Preparation token, title, summary, reason and compaction kind are required."
            );
        if (!request.ArchiveOriginals)
            throw new ValidationException(
                "This release requires verified archival of compaction sources.",
                "policy_denied"
            );
        if (request.SessionId is not null)
            ManagedStoragePathResolver.RequireIdentifier(request.SessionId);
        if (request.TaskId is not null)
            ManagedStoragePathResolver.RequireIdentifier(request.TaskId);

        var requestHash = RequestHash("compact", request);
        if (await PriorAsync(request.OperationId, requestHash, cancellationToken) is { } replay)
            return CompactionResultFor(request, replay);

        var snapshot = await catalog.ReadAsync(cancellationToken);
        var sources = await ReadCompactionSourcesAsync(snapshot, request.Sources, cancellationToken);
        if (CompactionToken(sources) != request.PreparationToken)
            throw new ValidationException(
                "Compaction sources changed after preparation.",
                "revision_conflict"
            );
        ValidateCompactionCoverage(request, sources);
        foreach (var source in sources.Where(source => source.Protected))
            grants.Require(source, OperatorAction.ArchiveProtectedMemory);

        var now = time.GetUtcNow();
        var details = new CompactionDetails
        {
            Kind = request.Kind,
            SourceMemoryIds = sources.Select(source => source.Id).ToList(),
            Summary = request.Summary,
            Facts = request.Facts,
            OpenQuestions = request.OpenQuestions,
            Contradictions = request.Contradictions,
            SuccessfulPatterns = request.SuccessfulPatterns,
            FailedPatterns = request.FailedPatterns,
            CompactedAtUtc = now,
            AgentId = request.AgentId,
            Reason = request.Reason,
            Evidence = request.Evidence,
            SourceCoverage = request.SourceCoverage
                .Select(item => new CompactionSourceCoverage
                {
                    MemoryId = item.MemoryId,
                    Evidence = item.Evidence,
                    Contradictions = item.Contradictions,
                })
                .ToList(),
        };
        var structured = JsonSerializer.SerializeToElement(details, MemoryJson.Options);
        var compacted = new MemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            RepositoryId = paths.RepositoryId,
            Tier = MemoryTier.Short,
            SessionId = request.SessionId,
            TaskId = request.TaskId,
            AgentId = request.AgentId,
            Category = "compacted_summary",
            DecisionArea = request.DecisionArea,
            Title = request.Title,
            Content = request.Summary,
            StructuredData = structured,
            Tags = request.Tags.Distinct(StringComparer.Ordinal).ToList(),
            SourceType = "compaction",
            SourceReferences = sources
                .Select(source => "memory:" + source.Id)
                .Concat(request.Evidence)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ReviewAfterUtc = now.AddDays(options.Value.Short.ReviewAfterDays),
            LastReviewedAtUtc = now,
            ReviewState = ReviewState.Reviewed,
            Status = MemoryState.Compacted,
            ReviewReason = request.Reason,
            ReviewEvidence = request.Evidence,
            ContentHash = MemoryClaimHash.Content(request.Summary, structured),
            OperationId = request.OperationId,
            Protected = sources.Any(source => source.Protected),
            EmbeddingState = EmbeddingState.NotRequired,
            IndexState = VectorIndexState.NotRequired,
            Compaction = details,
            Relationships = sources
                .Select(source => new MemoryRelationship(RelationshipKind.CompactedFrom, source.Id))
                .Concat(
                    request.Kind == CompactionKind.ReviewedDuplicateMerge
                        ? sources.Select(source =>
                            new MemoryRelationship(RelationshipKind.DuplicateOf, source.Id)
                        )
                        : []
                )
                .Distinct()
                .ToList(),
            Provenance = sources
                .Select(source => new MemoryProvenance(
                    source.Id,
                    source.RepositoryId,
                    source.Revision,
                    source.ContentHash,
                    source.SessionId,
                    source.TaskId,
                    source.AgentId,
                    source.CreatedAtUtc
                ))
                .ToList(),
        };
        MemoryRecordValidator.Validate(compacted);
        requests.Validate(compacted, content: true);

        var archiveId = "archive-" + ManagedTransactionStore.Hash(request.OperationId)[..24];
        var month = sources.Min(source => source.CreatedAtUtc).ToString(
            "yyyy-MM",
            System.Globalization.CultureInfo.InvariantCulture
        );
        var archiveParts = new[] { "short-term", "archive", month, archiveId + ".jsonl.gz" };
        var manifestParts = new[] { "short-term", "archive", month, archiveId + ".manifest.json" };
        var manifestFile = new ManagedFile(ManagedArea.Learning, null, manifestParts);
        ArchiveManifest manifest;
        var existingManifest = await store.ReadAsync(manifestFile, cancellationToken);
        if (existingManifest is null)
        {
            var lines = sources.Select(SerializeArchiveRecord).ToArray();
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
            manifest = new(
                1,
                archiveId,
                paths.RepositoryId,
                now,
                "jsonl+gzip",
                expanded.Length,
                compressed.Length,
                sources.Select((source, index) => new ArchiveManifestEntry(
                        source.Id,
                        source.Revision,
                        ManagedTransactionStore.Hash(lines[index]),
                        source.ContentHash,
                        index
                    ))
                    .ToDictionary(entry => entry.MemoryId, StringComparer.Ordinal)
            );
            await store.CommitAsync(
                "compact-archive-stage-" + ManagedTransactionStore.Hash(request.OperationId),
                [
                    new(
                        new(ManagedArea.Learning, null, archiveParts),
                        null,
                        null,
                        null,
                        Convert.ToBase64String(compressed)
                    ),
                    new(manifestFile, Serialize(manifest), null),
                ],
                cancellationToken
            );
        }
        else
        {
            manifest = JsonSerializer.Deserialize<ArchiveManifest>(existingManifest, MemoryJson.Options)
                ?? throw new ValidationException("Archive manifest is invalid.", "storage_invalid");
            if (
                manifest.ArchiveId != archiveId
                || manifest.Records.Count != sources.Count
                || sources.Any(source =>
                    !manifest.Records.TryGetValue(source.Id, out var entry)
                    || entry.Revision != source.Revision
                    || entry.ContentHash != source.ContentHash
                )
            )
                throw new ValidationException(
                    "Compaction operation ID belongs to different sources.",
                    "operation_conflict"
                );
        }
        await VerifyArchiveAsync(archiveParts, manifest, cancellationToken);

        var archiveIndexJson = await store.ReadAsync(ArchiveIndexFile, cancellationToken);
        var archiveIndex = archiveIndexJson is null
            ? new ArchiveIndex(1, new(StringComparer.Ordinal))
            : JsonSerializer.Deserialize<ArchiveIndex>(archiveIndexJson, MemoryJson.Options)
                ?? throw new ValidationException("Archive index is invalid.", "storage_invalid");
        if (archiveIndex.SchemaVersion != 1)
            throw new ValidationException("Archive index version is unsupported.", "storage_invalid");
        foreach (var source in sources)
            archiveIndex.Records[source.Id] = new(
                manifestParts,
                archiveId,
                manifest.CreatedAtUtc
            );

        var sourceJson = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
            sourceJson[source.Id] =
                await store.ReadAsync(LearningCatalog.FileFor(source), cancellationToken)
                ?? throw new ValidationException(
                    "Compaction source disappeared.",
                    "revision_conflict"
                );
        var files = sources
            .Select(source => new ManagedMutation(
                LearningCatalog.FileFor(source),
                null,
                ManagedTransactionStore.Hash(sourceJson[source.Id])
            ))
            .ToList();
        files.Add(new(LearningCatalog.FileFor(compacted), Serialize(compacted), null));
        files.Add(
            new(
                ArchiveIndexFile,
                Serialize(archiveIndex),
                archiveIndexJson is null ? null : ManagedTransactionStore.Hash(archiveIndexJson)
            )
        );
        var writeResult = new MemoryWriteResult(
            compacted.Id,
            compacted.Revision,
            compacted.Tier,
            compacted.Status
        );
        await CommitAsync(
            request.OperationId,
            requestHash,
            writeResult,
            snapshot,
            [compacted],
            sources.Select(source => source.Id).ToArray(),
            files,
            request.AgentId,
            request.Kind == CompactionKind.ReviewedDuplicateMerge
                ? "memory-duplicates-merged"
                : "memory-compacted",
            cancellationToken,
            request.Reason,
            compacted,
            () =>
            {
                foreach (var source in sources.Where(source => source.Protected))
                    grants.Require(source, OperatorAction.ArchiveProtectedMemory);
            }
        );
        return CompactionResultFor(request, writeResult);
    }

    private async Task<List<MemoryRecord>> ReadCompactionSourcesAsync(
        CatalogSnapshot snapshot,
        IReadOnlyList<ArchiveMemoryItem> requested,
        CancellationToken cancellationToken
    )
    {
        var records = new List<MemoryRecord>(requested.Count);
        foreach (var source in requested)
        {
            var record = await ForMutationAsync(
                snapshot,
                source.MemoryId,
                source.ExpectedRevision,
                cancellationToken
            );
            if (record.Tier != MemoryTier.Short || !IsOrdinarilyActive(LearningCatalog.EntryFor(record)))
                throw new ValidationException("Compaction sources must be active short-term memory.");
            if (record.ReviewState == ReviewState.Unreviewed)
                throw new ValidationException(
                    "Every compaction source requires an explicit review decision.",
                    "review_required"
                );
            records.Add(record);
        }
        return records;
    }

    private void ValidateCompactionCoverage(
        CommitCompactionRequest request,
        IReadOnlyList<MemoryRecord> sources
    )
    {
        var sourceIds = sources.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
        if (
            request.SourceCoverage.Count != sourceIds.Count
            || request.SourceCoverage.Select(item => item.MemoryId).Distinct(StringComparer.Ordinal).Count()
                != sourceIds.Count
            || request.SourceCoverage.Any(item => !sourceIds.Contains(item.MemoryId))
        )
            throw new ValidationException(
                "Source coverage must contain every source exactly once.",
                "source_coverage_incomplete"
            );
        var globalEvidence = request.Evidence.ToHashSet(StringComparer.Ordinal);
        var globalContradictions = request.Contradictions.ToHashSet(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var coverage = request.SourceCoverage.Single(item => item.MemoryId == source.Id);
            if (coverage.Evidence.Any(item => !globalEvidence.Contains(item)))
                throw new ValidationException(
                    "Source coverage evidence must be preserved in the compacted evidence list.",
                    "source_coverage_incomplete"
                );
            if (coverage.Contradictions.Any(item => !globalContradictions.Contains(item)))
                throw new ValidationException(
                    "Source contradictions must be preserved in the compacted contradiction list.",
                    "source_coverage_incomplete"
                );
            var requiredEvidence = source.ReviewEvidence
                .Concat(source.SourceReferences)
                .Concat(source.Compaction?.Evidence ?? [])
                .Distinct(StringComparer.Ordinal);
            if (requiredEvidence.Any(item => !coverage.Evidence.Contains(item, StringComparer.Ordinal)))
                throw new ValidationException(
                    "Source evidence is missing from compaction coverage.",
                    "source_coverage_incomplete"
                );
            var requiredContradictions = source.Compaction?.Contradictions ?? [];
            if (
                requiredContradictions.Any(item =>
                    !coverage.Contradictions.Contains(item, StringComparer.Ordinal)
                )
                || (source.Contradicted && coverage.Contradictions.Count == 0)
            )
                throw new ValidationException(
                    "An unresolved source contradiction is missing from compaction coverage.",
                    "source_coverage_incomplete"
                );
        }
    }

    private static void ValidateCompactionSources(
        string operationId,
        string agentId,
        IReadOnlyList<ArchiveMemoryItem> sources
    )
    {
        ManagedStoragePathResolver.RequireIdentifier(operationId);
        ManagedStoragePathResolver.RequireIdentifier(agentId);
        if (sources.Count < 2)
            throw new ValidationException("Compaction requires at least two sources.");
        if (sources.Select(source => source.MemoryId).Distinct(StringComparer.Ordinal).Count() != sources.Count)
            throw new ValidationException("Compaction source IDs must be unique.");
        foreach (var source in sources)
        {
            ManagedStoragePathResolver.RequireIdentifier(source.MemoryId);
            if (source.ExpectedRevision < 1)
                throw new ValidationException("Compaction revisions must be positive.");
        }
    }

    private string CompactionToken(IEnumerable<MemoryRecord> sources) =>
        ManagedTransactionStore.Hash(
            "compaction-v1\n"
                + paths.RepositoryId
                + "\n"
                + string.Join(
                    "\n",
                    sources.OrderBy(source => source.Id, StringComparer.Ordinal)
                        .Select(source => $"{source.Id}:{source.Revision}:{source.ContentHash}")
                )
        );

    private static CompactionResult CompactionResultFor(
        CommitCompactionRequest request,
        MemoryWriteResult result
    )
    {
        var sourceIds = request.Sources.Select(source => source.MemoryId).ToArray();
        return new(
            result.MemoryId,
            result.Revision,
            sourceIds,
            "archive-" + ManagedTransactionStore.Hash(request.OperationId)[..24],
            sourceIds,
            request.Contradictions,
            request.Kind
        );
    }
}
