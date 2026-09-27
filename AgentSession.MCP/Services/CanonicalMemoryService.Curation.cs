using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Services;

public sealed partial class CanonicalMemoryService
{
    public async Task<RecallMemoryResult> RecallAsync(
        RecallMemoryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        requests.Validate(request, content: true);
        foreach (var count in new[]
        {
            request.Tiers.Count,
            request.Categories.Count,
            request.DecisionAreas.Count,
            request.Tags.Count,
            request.Statuses.Count,
            request.MemoryIds.Count,
        })
            requests.ValidateOptionalBatch(count);
        if (
            request.MaxResults < 1
            || request.MaxResults > options.Value.Limits.MaxRecallResults
            || request.Tiers.Count == 0
            || request.Tiers.Any(tier => !Enum.IsDefined(tier))
            || request.Statuses.Any(status => !Enum.IsDefined(status))
            || request.MinimumSimilarity is < -1 or > 1
            || (request.MinimumSimilarity.HasValue
                && !double.IsFinite(request.MinimumSimilarity.Value))
        )
            throw new ValidationException("Invalid recall bounds or tiers.");
        foreach (var id in request.MemoryIds)
            ManagedStoragePathResolver.RequireIdentifier(id);
        await CleanupAsync(cancellationToken);
        var snapshot = await catalog.ReadAsync(cancellationToken);
        var matches = new List<RecalledMemory>();
        var semanticCandidates = new List<MemoryRecord>();
        var health = new List<string>();
        var words = request.Query.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        foreach (var entry in snapshot.Entries.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (
                !request.Tiers.Contains(entry.Tier)
                || IsExpired(entry)
                || !LifecycleAllowed(entry.Status, request)
            )
                continue;
            var record = await ReadCanonicalAsync(entry, cancellationToken);
            if (
                IsExpired(LearningCatalog.EntryFor(record))
                || (!request.IncludeHistory && (record.Stale || record.Contradicted))
                || !MatchesRecallFilters(record, request)
            )
                continue;
            if (words.Length > 0 && record.Tier == MemoryTier.Long)
                semanticCandidates.Add(record);
            else
                AddLexicalMatch(matches, record, words);
        }

        var semanticUsed = false;
        if (
            words.Length > 0
            && semanticCandidates.Count > 0
            && matches.Count < request.MaxResults
        )
        {
            try
            {
                var active = await ReadActiveVectorIdentityAsync(cancellationToken);
                if (active is null)
                    throw new SemanticDependencyException("semantic_active_collection_missing");
                var model = await embeddings.GetModelIdentityAsync(cancellationToken);
                if (
                    active.Fingerprint.Provider != "ollama"
                    || active.Fingerprint.Model != model.Model
                    || active.Fingerprint.ModelDigest != model.Digest
                )
                    throw new SemanticDependencyException("embedding_migration_required");
                var query = await embeddings.EmbedAsync(
                    [request.Query],
                    active.Fingerprint.Dimension,
                    cancellationToken
                );
                var observed = EmbeddingProjection.Fingerprint(
                    "ollama",
                    model.Model,
                    model.Digest,
                    query.Vectors.Single().Length
                );
                if (observed.Hash != active.Fingerprint.Hash)
                    throw new SemanticDependencyException("embedding_migration_required");
                var allowedStatuses = semanticCandidates
                    .Select(record => EnumValue(record.Status))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var categoryFilters = RequestedValues(request.Category, request.Categories);
                var hits = await vectors.SearchAsync(
                    active,
                    paths.RepositoryId,
                    query.Vectors.Single(),
                    new(
                        Statuses: allowedStatuses,
                        Categories: categoryFilters,
                        Tags: request.Tags,
                        DecisionArea: request.DecisionArea
                    ),
                    Math.Min(
                        options.Value.Limits.MaxRecallResults,
                        Math.Max(request.MaxResults, semanticCandidates.Count)
                    ),
                    request.MinimumSimilarity is null
                        ? options.Value.Long.MinimumSimilarity is null
                            ? null
                            : (float)options.Value.Long.MinimumSimilarity.Value
                        : (float)request.MinimumSimilarity.Value,
                    cancellationToken
                );
                foreach (var hit in hits)
                {
                    if (
                        matches.Count >= request.MaxResults
                        || !snapshot.Entries.TryGetValue(hit.MemoryId, out var entry)
                        || entry.Tier != MemoryTier.Long
                        || entry.Revision != hit.Revision
                        || entry.ContentHash != hit.ContentHash
                        || !LifecycleAllowed(entry.Status, request)
                    )
                        continue;
                    var record = await ReadCanonicalAsync(entry, cancellationToken);
                    if (
                        EnumValue(record.Status) != hit.Status
                        || record.Stale && !request.IncludeHistory
                        || record.Contradicted && !request.IncludeHistory
                        || !MatchesRecallFilters(record, request)
                    )
                        continue;
                    matches.Add(new(record, "semantic", hit.Score));
                }
                semanticUsed = true;
            }
            catch (SemanticDependencyException error)
            {
                health.Add(error.Code);
            }
            catch (TimeoutException)
            {
                health.Add("embedding_timeout");
            }
            catch (HttpRequestException)
            {
                health.Add("embedding_unavailable");
            }
            catch (Exception error)
                when (error is InvalidDataException or Grpc.Core.RpcException)
            {
                health.Add(
                    error is Grpc.Core.RpcException
                        ? "vector_index_unavailable"
                        : "embedding_or_index_invalid"
                );
            }
            if (!semanticUsed)
                foreach (var record in semanticCandidates)
                    AddLexicalMatch(matches, record, words);
        }

        if (request.IncludeArchived && request.MemoryIds.Count > 0)
        {
            foreach (var id in request.MemoryIds)
            {
                if (snapshot.Entries.ContainsKey(id))
                    continue;
                var archived = await ReadArchivedAsync(id, cancellationToken);
                if (
                    archived.Record is { } record
                    && request.Tiers.Contains(record.Tier)
                    && MatchesRecallFilters(record, request)
                )
                    AddLexicalMatch(matches, record, words, "archived");
            }
        }
        var ordered = matches
            .OrderBy(match => match.Record.Tier)
            .ThenByDescending(match => match.Score)
            .ThenByDescending(match => match.Record.UpdatedAtUtc)
            .ThenBy(match => match.Record.Id, StringComparer.Ordinal)
            .ToArray();
        return new(
            ordered.DistinctBy(match => match.Record.Id).Take(request.MaxResults).ToArray(),
            ordered.Select(match => match.Record.Id).Distinct().Count() > request.MaxResults,
            words.Length == 0
                ? "recent"
                : semanticUsed
                    ? "semantic"
                    : "degraded_lexical",
            snapshot.Manifest.InvalidRecordIds.Count > 0
                ? [.. health.Distinct(), "invalid_canonical_records_excluded"]
                : health.Distinct().ToArray()
        );
    }

    private async Task<VectorCollectionIdentity?> ReadActiveVectorIdentityAsync(
        CancellationToken cancellationToken
    )
    {
        var json = await store.ReadAsync(VectorMigrationService.ActiveFile, cancellationToken);
        if (json is null)
            return null;
        var active =
            JsonSerializer.Deserialize<ActiveVectorCollection>(json, MemoryJson.Options)
            ?? throw new InvalidDataException("Empty active vector collection metadata.");
        if (active.Active.RepositoryId != paths.RepositoryId)
            throw new InvalidDataException("Active vector repository is invalid.");
        return new(
            active.Active.RepositoryId,
            active.Active.Alias,
            active.Active.PhysicalCollection,
            active.Active.Fingerprint
        );
    }

    private static bool LifecycleAllowed(MemoryState status, RecallMemoryRequest request)
    {
        if (request.Statuses.Count > 0 && !request.Statuses.Contains(status))
            return false;
        return status switch
        {
            MemoryState.Active or MemoryState.Compacted or MemoryState.Validated => true,
            MemoryState.Candidate =>
                request.IncludeCandidates || request.Statuses.Contains(MemoryState.Candidate),
            MemoryState.Retired =>
                request.IncludeRetired
                || request.IncludeHistory
                || request.Statuses.Contains(MemoryState.Retired),
            MemoryState.Superseded or MemoryState.Rejected =>
                request.IncludeHistory || request.Statuses.Contains(status),
            MemoryState.Archived => request.IncludeArchived,
            _ => false,
        };
    }

    private static bool MatchesRecallFilters(MemoryRecord record, RecallMemoryRequest request)
    {
        var categories = RequestedValues(request.Category, request.Categories);
        var decisionAreas = RequestedValues(request.DecisionArea, request.DecisionAreas);
        return (request.MemoryIds.Count == 0 || request.MemoryIds.Contains(record.Id))
            && (request.SessionId is null || request.SessionId == record.SessionId)
            && (request.TaskId is null || request.TaskId == record.TaskId)
            && (request.ParentStepId is null || request.ParentStepId == record.ParentStepId)
            && (request.AgentId is null || request.AgentId == record.AgentId)
            && (categories.Count == 0 || categories.Contains(record.Category))
            && (
                decisionAreas.Count == 0
                || (
                    record.DecisionArea is not null
                    && decisionAreas.Contains(record.DecisionArea)
                )
            )
            && request.Tags.All(tag =>
                record.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)
            )
            && (request.SinceUtc is not { } since || record.CreatedAtUtc >= since)
            && (request.BeforeUtc is not { } before || record.CreatedAtUtc < before);
    }

    private static IReadOnlyList<string> RequestedValues(
        string? single,
        IReadOnlyCollection<string> multiple
    ) =>
        (single is null ? multiple : multiple.Append(single))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static void AddLexicalMatch(
        ICollection<RecalledMemory> matches,
        MemoryRecord record,
        IReadOnlyList<string> words,
        string? emptyMatchType = null
    )
    {
        var text =
            record.Title
            + "\n"
            + record.Content
            + "\n"
            + record.StructuredData?.GetRawText()
            + "\n"
            + string.Join(" ", record.Tags);
        var score =
            words.Count == 0
                ? 0
                : (double)
                    words.Count(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))
                    / words.Count;
        if (words.Count != 0 && score == 0)
            return;
        matches.Add(
            new(
                record,
                words.Count == 0 ? emptyMatchType ?? "recent" : "lexical",
                words.Count == 0 ? null : score
            )
        );
    }

    private static string EnumValue<T>(T value)
        where T : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    private sealed class SemanticDependencyException(string code) : Exception
    {
        public string Code { get; } = code;
    }

    public async Task<MemoryWriteResult> DeleteAsync(
        DeleteMemoryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ValidateAction(
            request.MemoryId,
            request.OperationId,
            request.ExpectedRevision,
            request.AgentId,
            request.Reason
        );
        requests.Validate(request, content: true);
        var hash = RequestHash("delete", request);
        if (await PriorAsync(request.OperationId, hash, cancellationToken) is { } prior)
            return prior;
        var snapshot = await catalog.ReadAsync(cancellationToken);
        var record = await ForMutationAsync(
            snapshot,
            request.MemoryId,
            request.ExpectedRevision,
            cancellationToken
        );
        if (record.Tier == MemoryTier.Long)
            throw new ValidationException(
                "Long-term learning must be retired, not physically deleted."
            );
        if (record.Protected)
            grants.Require(record, OperatorAction.DeleteProtectedMemory);
        var file = LearningCatalog.FileFor(record);
        var json = await store.ReadAsync(file, cancellationToken);
        var result = new MemoryWriteResult(
            record.Id,
            record.Revision + 1,
            record.Tier,
            MemoryState.Deleted
        );
        await CommitAsync(
            request.OperationId,
            hash,
            result,
            snapshot,
            [],
            [record.Id],
            [new(file, null, ManagedTransactionStore.Hash(json!))],
            request.AgentId,
            "memory-deleted",
            cancellationToken,
            request.Reason,
            record,
            () =>
            {
                if (record.Protected)
                    grants.Require(record, OperatorAction.DeleteProtectedMemory);
            }
        );
        return result;
    }

    public async Task<MemoryWriteResult> PromoteAsync(
        PromoteMemoryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ValidateAction(
            request.MemoryId,
            request.OperationId,
            request.ExpectedRevision,
            request.AgentId,
            request.Reason
        );
        requests.Validate(request, content: true);
        requests.ValidateOptionalBatch(request.VerificationEvidence?.Count ?? 0);
        var hash = RequestHash("promote", request);
        if (await PriorAsync(request.OperationId, hash, cancellationToken) is { } prior)
            return prior;
        var snapshot = await catalog.ReadAsync(cancellationToken);
        var source = await ForMutationAsync(
            snapshot,
            request.MemoryId,
            request.ExpectedRevision,
            cancellationToken
        );
        if (source.Tier == MemoryTier.Long)
            return await ValidateCandidateAsync(
                request,
                hash,
                snapshot,
                source,
                cancellationToken
            );
        if (source.PromotionState is not null)
            throw new ValidationException("Source has already been promoted.");
        var now = time.GetUtcNow();
        var destination = JsonSerializer.Deserialize<MemoryRecord>(
            Serialize(source),
            MemoryJson.Options
        )!;
        destination.Id = Guid.NewGuid().ToString("N");
        destination.Tier = source.Tier == MemoryTier.Temp ? MemoryTier.Short : MemoryTier.Long;
        destination.Status =
            destination.Tier == MemoryTier.Long ? MemoryState.Candidate : MemoryState.Active;
        destination.CreatedAtUtc = now;
        destination.UpdatedAtUtc = now;
        destination.ExpiresAtUtc = null;
        destination.Revision = 1;
        destination.ReviewAfterUtc =
            destination.Tier == MemoryTier.Short
                ? now.AddDays(options.Value.Short.ReviewAfterDays)
                : null;
        destination.OperationId = request.OperationId;
        destination.AgentId = request.AgentId;
        destination.EmbeddingState =
            destination.Tier == MemoryTier.Long
                ? EmbeddingState.PendingEmbedding
                : EmbeddingState.NotRequired;
        destination.IndexState =
            destination.Tier == MemoryTier.Long
                ? VectorIndexState.Pending
                : VectorIndexState.NotRequired;
        destination.Relationships.Add(new(RelationshipKind.PromotedFrom, source.Id));
        destination.SourceReferences.Add("memory:" + source.Id);
        destination.PromotionState = null;
        destination.Provenance.Add(
            new(
                source.Id,
                source.RepositoryId,
                source.Revision,
                source.ContentHash,
                source.SessionId,
                source.TaskId,
                source.AgentId,
                source.CreatedAtUtc
            )
        );
        if (destination.Tier == MemoryTier.Long)
            destination.Compaction = null;
        var duplicate = snapshot.Entries.Values.FirstOrDefault(entry =>
            entry.ScopeHash == MemoryClaimHash.Scope(destination) && IsOrdinarilyActive(entry)
        );
        string? destinationBefore = null;
        if (duplicate is not null)
        {
            destination = await ReadCanonicalAsync(duplicate, cancellationToken);
            destinationBefore = await store.ReadAsync(
                LearningCatalog.FileFor(destination),
                cancellationToken
            );
            destination.Relationships.Add(new(RelationshipKind.PromotedFrom, source.Id));
            destination.SourceReferences.Add("memory:" + source.Id);
            destination.Provenance.Add(
                new(
                    source.Id,
                    source.RepositoryId,
                    source.Revision,
                    source.ContentHash,
                    source.SessionId,
                    source.TaskId,
                    source.AgentId,
                    source.CreatedAtUtc
                )
            );
            destination.Protected |= source.Protected;
            destination.Revision++;
            destination.UpdatedAtUtc = now;
            destination.OperationId = request.OperationId;
        }
        var sourceFile = LearningCatalog.FileFor(source);
        var sourceBefore = await store.ReadAsync(sourceFile, cancellationToken);
        source.PromotionState = destination.Id;
        source.Revision++;
        source.UpdatedAtUtc = now;
        source.OperationId = request.OperationId;
        if (source.Tier == MemoryTier.Temp)
            source.Status = MemoryState.Promoted;
        MemoryRecordValidator.Validate(source);
        MemoryRecordValidator.Validate(destination);
        var result = new MemoryWriteResult(
            destination.Id,
            destination.Revision,
            destination.Tier,
            destination.Status,
            duplicate is not null
        );
        await CommitAsync(
            request.OperationId,
            hash,
            result,
            snapshot,
            [source, destination],
            [],
            [
                new(
                    sourceFile,
                    Serialize(source),
                    ManagedTransactionStore.Hash(sourceBefore!),
                    source.ExpiresAtUtc
                ),
                new(
                    LearningCatalog.FileFor(destination),
                    Serialize(destination),
                    destinationBefore is null
                        ? null
                        : ManagedTransactionStore.Hash(destinationBefore)
                ),
            ],
            request.AgentId,
            "memory-promoted",
            cancellationToken,
            request.Reason,
            source
        );
        return result;
    }

    private async Task<MemoryWriteResult> ValidateCandidateAsync(
        PromoteMemoryRequest request,
        string requestHash,
        CatalogSnapshot snapshot,
        MemoryRecord candidate,
        CancellationToken cancellationToken
    )
    {
        if (candidate.Status != MemoryState.Candidate)
            throw new ValidationException("Only a long-term candidate can be validated.");
        var authoritative = request.AuthoritativeVerification;
        if (
            authoritative
            && (
                !options.Value.Long.AllowAuthoritativeVerification
                || request.VerificationEvidence is not { Count: > 0 }
                || request.VerificationEvidence.Any(string.IsNullOrWhiteSpace)
            )
        )
            throw new ValidationException(
                "Authoritative verification is disabled or lacks reproducible evidence.",
                "validation_policy_failed"
            );
        if (
            !authoritative
            && candidate.IndependentConfirmations
                < options.Value.Long.RequiredIndependentConfirmations
        )
            throw new ValidationException(
                "Candidate has insufficient independent successful confirmations.",
                "validation_policy_failed"
            );
        var highRisk = IsHighRisk(candidate);
        var grantRecord = JsonSerializer.Deserialize<MemoryRecord>(
            Serialize(candidate),
            MemoryJson.Options
        )!;
        if (highRisk)
            grants.Require(grantRecord, OperatorAction.ValidateLearning);
        var oldFile = LearningCatalog.FileFor(candidate);
        var before = await store.ReadAsync(oldFile, cancellationToken)
            ?? throw new ValidationException("Candidate disappeared.", "revision_conflict");
        candidate.Status = MemoryState.Validated;
        candidate.ValidatedAtUtc = time.GetUtcNow();
        candidate.UpdatedAtUtc = time.GetUtcNow();
        candidate.AgentId = request.AgentId;
        candidate.OperationId = request.OperationId;
        candidate.Revision++;
        candidate.IndexState = VectorIndexState.Pending;
        candidate.SourceReferences.AddRange(
            (request.VerificationEvidence ?? []).Select(reference => "verification:" + reference)
        );
        candidate.SourceReferences = candidate.SourceReferences
            .Distinct(StringComparer.Ordinal)
            .ToList();
        MemoryRecordValidator.Validate(candidate);
        var result = new MemoryWriteResult(
            candidate.Id,
            candidate.Revision,
            candidate.Tier,
            candidate.Status
        );
        await CommitAsync(
            request.OperationId,
            requestHash,
            result,
            snapshot,
            [candidate],
            [],
            [
                new(oldFile, null, ManagedTransactionStore.Hash(before)),
                new(LearningCatalog.FileFor(candidate), Serialize(candidate), null),
            ],
            request.AgentId,
            "memory-validated",
            cancellationToken,
            request.Reason,
            candidate,
            () =>
            {
                if (highRisk)
                    grants.Require(grantRecord, OperatorAction.ValidateLearning);
            }
        );
        return result;
    }

    private static bool IsHighRisk(MemoryRecord record)
    {
        var text = string.Join(
            ' ',
            new[] { record.Category, record.DecisionArea, record.Title, record.Content }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Concat(record.Tags)
        );
        string[] terms =
        [
            "security",
            "authentication",
            "authorization",
            "secret",
            "destructive",
            "migration",
            "deployment",
            "architecture",
            "public contract",
            "sensitive data",
        ];
        return terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<MemoryWriteResult> ReviewAsync(
        ReviewMemoryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ValidateAction(
            request.MemoryId,
            request.OperationId,
            request.ExpectedRevision,
            request.AgentId,
            request.Reason
        );
        requests.Validate(request, content: true);
        requests.ValidateBatch(request.Evidence.Count);
        if (
            request.Action
            is not (
                ReviewState.Keep
                or ReviewState.CompactCandidate
                or ReviewState.PromotionCandidate
                or ReviewState.ArchiveCandidate
                or ReviewState.DeleteCandidate
            )
        )
            throw new ValidationException("Unknown review action.");
        var hash = RequestHash("review", request);
        if (await PriorAsync(request.OperationId, hash, cancellationToken) is { } prior)
            return prior;
        var snapshot = await catalog.ReadAsync(cancellationToken);
        var record = await ForMutationAsync(
            snapshot,
            request.MemoryId,
            request.ExpectedRevision,
            cancellationToken
        );
        if (record.Tier != MemoryTier.Short)
            throw new ValidationException("Review actions target short-term learning.");
        var file = LearningCatalog.FileFor(record);
        var before = await store.ReadAsync(file, cancellationToken);
        record.ReviewState = request.Action;
        record.ReviewReason = request.Reason;
        record.ReviewEvidence = request.Evidence;
        record.LastReviewedAtUtc = time.GetUtcNow();
        if (request.Action == ReviewState.Keep) record.ReviewAfterUtc = time.GetUtcNow().AddDays(options.Value.Short.ReviewAfterDays);
        record.UpdatedAtUtc = time.GetUtcNow();
        record.AgentId = request.AgentId;
        record.Revision++;
        record.OperationId = request.OperationId;
        var result = new MemoryWriteResult(record.Id, record.Revision, record.Tier, record.Status);
        await CommitAsync(
            request.OperationId,
            hash,
            result,
            snapshot,
            [record],
            [],
            [new(file, Serialize(record), ManagedTransactionStore.Hash(before!))],
            request.AgentId,
            "memory-reviewed",
            cancellationToken,
            request.Reason
        );
        return result;
    }

    public async Task<IReadOnlyList<ReviewMemoryCandidate>> ListReviewAsync(
        ReviewListRequest request,
        CancellationToken cancellationToken = default
    )
    {
        requests.Validate(request, content: true);
        if (
            request.MaxResults < 1
            || request.MaxResults > options.Value.Limits.MaxRecallResults
            || (request.ReviewState.HasValue && !Enum.IsDefined(request.ReviewState.Value))
        )
            throw new ValidationException("Invalid review list bounds or state.");
        var snapshot = await catalog.ReadAsync(cancellationToken);
        var results = new List<ReviewMemoryCandidate>();
        foreach (
            var entry in snapshot
                .Entries.Values.Where(entry =>
                    entry.Tier == MemoryTier.Short && IsOrdinarilyActive(entry)
                )
                .OrderBy(entry => entry.CreatedAtUtc)
        )
        {
            var record = await ReadCanonicalAsync(entry, cancellationToken);
            if (
                (request.OlderThanUtc is { } older && record.CreatedAtUtc >= older)
                || (request.ReviewState is { } state && record.ReviewState != state)
                || (request.Category is not null && record.Category != request.Category)
                || (request.DecisionArea is not null && record.DecisionArea != request.DecisionArea)
                || (request.AgentId is not null && record.AgentId != request.AgentId)
                || (request.TaskId is not null && record.TaskId != request.TaskId)
            )
                continue;
            if (request.ReviewState is null && record.ReviewAfterUtc > time.GetUtcNow())
                continue;
            var age = time.GetUtcNow() - record.CreatedAtUtc;
            var (suggested, reason, approved) = record.ReviewState switch
            {
                not ReviewState.Unreviewed =>
                    (record.ReviewState, "explicit_review_state", true),
                _ when age >= TimeSpan.FromDays(options.Value.Short.ArchiveAfterDays) =>
                    (ReviewState.ArchiveCandidate, "archive_age_reached", false),
                _ when age >= TimeSpan.FromDays(options.Value.Short.CompactionCandidateAfterDays) =>
                    (ReviewState.CompactCandidate, "compaction_age_reached", false),
                _ => (ReviewState.Unreviewed, "review_due", false),
            };
            results.Add(new(record, suggested, reason, approved));
            if (results.Count == request.MaxResults)
                break;
        }
        return results;
    }

    public Task<MemoryWriteResult> SupersedeAsync(
        LongTermLifecycleRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request.ReplacementMemoryId is null)
            throw new ValidationException("Supersession requires a replacement memory ID.");
        return TransitionLongTermAsync(
            request,
            MemoryState.Superseded,
            "memory-superseded",
            cancellationToken
        );
    }

    public Task<MemoryWriteResult> RetireAsync(
        LongTermLifecycleRequest request,
        CancellationToken cancellationToken = default
    ) =>
        TransitionLongTermAsync(
            request,
            MemoryState.Retired,
            "memory-retired",
            cancellationToken
        );

    private async Task<MemoryWriteResult> TransitionLongTermAsync(
        LongTermLifecycleRequest request,
        MemoryState target,
        string eventType,
        CancellationToken cancellationToken
    )
    {
        ValidateAction(
            request.MemoryId,
            request.OperationId,
            request.ExpectedRevision,
            request.AgentId,
            request.Reason
        );
        requests.Validate(request, content: true);
        requests.ValidateBatch(request.Evidence.Count);
        if (request.Evidence.Count == 0 || request.Evidence.Any(string.IsNullOrWhiteSpace))
            throw new ValidationException("Lifecycle transition requires evidence.");
        if (request.ReplacementMemoryId is not null)
        {
            ManagedStoragePathResolver.RequireIdentifier(request.ReplacementMemoryId);
            if (request.ReplacementMemoryId == request.MemoryId)
                throw new ValidationException("A memory cannot replace itself.");
        }
        var requestHash = RequestHash(target.ToString(), request);
        if (await PriorAsync(request.OperationId, requestHash, cancellationToken) is { } prior)
            return prior;
        var snapshot = await catalog.ReadAsync(cancellationToken);
        var source = await ForMutationAsync(
            snapshot,
            request.MemoryId,
            request.ExpectedRevision,
            cancellationToken
        );
        if (
            source.Tier != MemoryTier.Long
            || source.Status is MemoryState.Superseded or MemoryState.Retired or MemoryState.Rejected
        )
            throw new ValidationException("Only active long-term learning can transition.");
        var oldFile = LearningCatalog.FileFor(source);
        var oldJson = await store.ReadAsync(oldFile, cancellationToken)
            ?? throw new ValidationException("Lifecycle source disappeared.", "revision_conflict");
        var updated = new List<MemoryRecord> { source };
        var mutations = new List<ManagedMutation>();
        MemoryRecord? replacement = null;
        if (request.ReplacementMemoryId is not null)
        {
            if (
                !snapshot.Entries.TryGetValue(request.ReplacementMemoryId, out var replacementEntry)
                || !IsOrdinarilyActive(replacementEntry)
            )
                throw new ValidationException("Replacement memory is unavailable.");
            replacement = await ReadCanonicalAsync(replacementEntry, cancellationToken);
            if (replacement.Tier != MemoryTier.Long)
                throw new ValidationException("Replacement must be long-term learning.");
            var replacementFile = LearningCatalog.FileFor(replacement);
            var replacementJson = await store.ReadAsync(replacementFile, cancellationToken)
                ?? throw new ValidationException("Replacement disappeared.", "revision_conflict");
            replacement.Relationships.Add(new(RelationshipKind.Supersedes, source.Id));
            replacement.SourceReferences.Add("memory:" + source.Id);
            replacement.Relationships = replacement.Relationships.Distinct().ToList();
            replacement.SourceReferences = replacement.SourceReferences
                .Distinct(StringComparer.Ordinal)
                .ToList();
            replacement.Revision++;
            replacement.UpdatedAtUtc = time.GetUtcNow();
            replacement.OperationId = request.OperationId;
            replacement.IndexState = VectorIndexState.Pending;
            MemoryRecordValidator.Validate(replacement);
            updated.Add(replacement);
            mutations.Add(
                new(
                    replacementFile,
                    Serialize(replacement),
                    ManagedTransactionStore.Hash(replacementJson)
                )
            );
            source.Relationships.Add(new(RelationshipKind.SupersededBy, replacement.Id));
        }
        source.Relationships = source.Relationships.Distinct().ToList();
        source.Status = target;
        source.Stale = true;
        source.ReviewReason = request.Reason;
        source.ReviewEvidence = request.Evidence;
        source.AgentId = request.AgentId;
        source.UpdatedAtUtc = time.GetUtcNow();
        source.OperationId = request.OperationId;
        source.Revision++;
        source.IndexState = VectorIndexState.Pending;
        MemoryRecordValidator.Validate(source);
        mutations.Add(new(oldFile, null, ManagedTransactionStore.Hash(oldJson)));
        mutations.Add(new(LearningCatalog.FileFor(source), Serialize(source), null));
        var result = new MemoryWriteResult(
            source.Id,
            source.Revision,
            source.Tier,
            source.Status
        );
        await CommitAsync(
            request.OperationId,
            requestHash,
            result,
            snapshot,
            updated,
            [],
            mutations,
            request.AgentId,
            eventType,
            cancellationToken,
            request.Reason,
            source
        );
        return result;
    }

    private async Task<MemoryRecord> ForMutationAsync(
        CatalogSnapshot snapshot,
        string id,
        long revision,
        CancellationToken cancellationToken
    )
    {
        if (!snapshot.Entries.TryGetValue(id, out var entry) || IsExpired(entry))
            throw new ValidationException("Memory not found or expired.");
        var record = await ReadCanonicalAsync(entry, cancellationToken);
        if (record.Revision != revision)
            throw new ValidationException(
                $"Memory revision conflict: expected {revision}, current {record.Revision}; reread before retrying.",
                "revision_conflict"
            );
        return record;
    }

    private static void ValidateAction(
        string id,
        string operationId,
        long revision,
        string actor,
        string reason
    )
    {
        ManagedStoragePathResolver.RequireIdentifier(id);
        ManagedStoragePathResolver.RequireIdentifier(operationId);
        ManagedStoragePathResolver.RequireIdentifier(actor);
        if (revision < 1 || string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("Positive expected revision and rationale are required.");
    }
}
