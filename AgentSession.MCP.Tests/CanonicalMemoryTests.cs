using System.Diagnostics;
using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Extensions;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;
using AgentSession.MCP.Observability;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace AgentSession.MCP.Tests;

public sealed class CanonicalMemoryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "canonical-memory-tests",
        Guid.NewGuid().ToString("N")
    );
    private readonly Clock _time = new();

    private ServiceProvider Build(
        double lifetime = 24,
        int journalBytes = 262_144,
        int maxContentBytes = 65_536,
        int maxRecordBytes = 1_048_576,
        int archiveCompressedBytes = 16_777_216,
        int archiveExpandedBytes = 67_108_864,
        bool allowAuthoritativeVerification = false
    ) =>
        new ServiceCollection()
            .AddSingleton<TimeProvider>(_time)
            .AddMemoryStorageConfiguration(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["SystemStorage:Root"] = _root,
                            ["Repository:Id"] = "repo",
                            ["Memory:Temp:MaxAgeHours"] = lifetime.ToString(
                                System.Globalization.CultureInfo.InvariantCulture
                            ),
                            ["Memory:Limits:MaxEventJournalBytes"] = journalBytes.ToString(
                                System.Globalization.CultureInfo.InvariantCulture
                            ),
                            ["Memory:Limits:MaxContentBytes"] = maxContentBytes.ToString(
                                System.Globalization.CultureInfo.InvariantCulture
                            ),
                            ["Memory:Limits:MaxRecordBytes"] = maxRecordBytes.ToString(
                                System.Globalization.CultureInfo.InvariantCulture
                            ),
                            ["Memory:Limits:MaxArchiveCompressedBytes"] = archiveCompressedBytes.ToString(
                                System.Globalization.CultureInfo.InvariantCulture
                            ),
                            ["Memory:Limits:MaxArchiveExpandedBytes"] = archiveExpandedBytes.ToString(
                                System.Globalization.CultureInfo.InvariantCulture
                            ),
                            ["Memory:Long:AllowAuthoritativeVerification"] =
                                allowAuthoritativeVerification.ToString(),
                        }
                    )
                    .Build()
            )
            .BuildServiceProvider();

    private static RememberMemoryRequest Remember(
        string operation,
        RememberTier tier = RememberTier.Temp
    ) =>
        new()
        {
            OperationId = operation,
            Tier = tier,
            Content = "Parser handles Unicode input",
            SessionId = "session",
            AgentId = "agent-a",
            TaskId = "task",
        };

    [Fact]
    public async Task RestartPreservesTempExpiryAndCleanupIsIdempotent()
    {
        string id;
        DateTimeOffset? expiry;
        using (var provider = Build())
        {
            var service = provider.GetRequiredService<CanonicalMemoryService>();
            var saved = await service.RememberAsync(Remember("remember"));
            id = saved.MemoryId;
            var read = await service.GetAsync(new(id));
            expiry = read.Record!.ExpiresAtUtc;
            Assert.Equal(_time.GetUtcNow().AddHours(24), expiry);
            Assert.Equal(EmbeddingState.NotRequired, read.Record.EmbeddingState);
            Assert.Equal(VectorIndexState.NotRequired, read.Record.IndexState);
            Assert.Contains(
                id + ".json",
                Directory
                    .GetFiles(_root, "*.json", SearchOption.AllDirectories)
                    .Select(Path.GetFileName)
            );
        }
        _time.Advance(TimeSpan.FromHours(23));
        using var restarted = Build();
        var resumed = restarted.GetRequiredService<CanonicalMemoryService>();
        Assert.Equal(expiry, (await resumed.GetAsync(new(id))).Record!.ExpiresAtUtc);
        _time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(1, (await resumed.CleanupAsync()).Deleted);
        Assert.Null((await resumed.GetAsync(new(id))).Record);
        Assert.Equal(0, (await resumed.CleanupAsync()).Deleted);
        Assert.Empty(Directory.GetFiles(_root, id + ".json", SearchOption.AllDirectories));
        Assert.Equal(id, (await resumed.RememberAsync(Remember("remember"))).MemoryId);
        Assert.Empty(Directory.GetFiles(_root, id + ".json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task TempUpdatesCannotExtendExpiryAndRespectRevision()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var saved = await service.RememberAsync(Remember("remember"));
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateAsync(
                new(
                    saved.MemoryId,
                    "extend",
                    1,
                    "agent-a",
                    "New content",
                    ExpiresAtUtc: _time.GetUtcNow().AddHours(25)
                )
            )
        );
        var update = await service.UpdateAsync(
            new(
                saved.MemoryId,
                "update",
                1,
                "agent-a",
                "New content",
                ExpiresAtUtc: _time.GetUtcNow().AddHours(2)
            )
        );
        Assert.Equal(2, update.Revision);
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateAsync(new(saved.MemoryId, "stale", 1, "agent-a", "Old edit"))
        );
        _time.Advance(TimeSpan.FromHours(2));
        Assert.Null((await service.GetAsync(new(saved.MemoryId))).Record);
    }

    [Fact]
    public async Task DerivedIndexRepairsWithoutChangingCanonicalContent()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var saved = await service.RememberAsync(Remember("remember", RememberTier.Short));
        var file = Assert.Single(
            Directory.GetFiles(_root, saved.MemoryId + ".json", SearchOption.AllDirectories)
        );
        var original = await File.ReadAllBytesAsync(file);
        var index = Path.Combine(
            provider.GetRequiredService<MemoryStoragePaths>().LearningRoot,
            ".index.json"
        );
        await File.WriteAllTextAsync(index, "invalid derived index");
        Assert.Equal(
            "Parser handles Unicode input",
            (await service.GetAsync(new(saved.MemoryId))).Record!.Content
        );
        Assert.Equal(original, await File.ReadAllBytesAsync(file));
        _time.Advance(TimeSpan.FromDays(30));
        Assert.NotNull((await service.GetAsync(new(saved.MemoryId))).Record);
    }

    [Fact]
    public async Task HashNormalizationDeduplicatesWithinContextOnly()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var first = Remember("first");
        first.Content = "caf\u00e9\r\nclaim";
        first.StructuredData = JsonSerializer.SerializeToElement(new { b = 2, a = 1 });
        var one = await service.RememberAsync(first);
        var equivalent = Remember("equivalent");
        equivalent.Content = "cafe\u0301\nclaim";
        equivalent.StructuredData = JsonSerializer.SerializeToElement(new { a = 1, b = 2 });
        var duplicate = await service.RememberAsync(equivalent);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(one.MemoryId, duplicate.MemoryId);
        equivalent.OperationId = "other-session";
        equivalent.SessionId = "other";
        Assert.NotEqual(one.MemoryId, (await service.RememberAsync(equivalent)).MemoryId);
        equivalent.OperationId = "different-claim";
        equivalent.SessionId = "session";
        equivalent.Content += " contradicted";
        Assert.NotEqual(one.MemoryId, (await service.RememberAsync(equivalent)).MemoryId);
    }

    [Fact]
    public async Task CleanupPreservesUnknownSchemaInsteadOfTrustingExpiredIndex()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var saved = await service.RememberAsync(Remember("remember"));
        var file = Assert.Single(
            Directory.GetFiles(_root, saved.MemoryId + ".json", SearchOption.AllDirectories)
        );
        var json = await File.ReadAllTextAsync(file);
        await File.WriteAllTextAsync(
            file,
            json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2")
        );
        _time.Advance(TimeSpan.FromDays(1));
        var result = await service.CleanupAsync();
        Assert.Equal(0, result.Deleted);
        Assert.Equal(1, result.Errors);
        Assert.True(File.Exists(file));
        Assert.Null((await service.GetAsync(new(saved.MemoryId))).Record);
    }

    [Fact]
    public async Task PromotionRecoversWithProvenanceAndTempExpiryDoesNotDeleteShortResult()
    {
        string sourceId;
        PromoteMemoryRequest promotion;
        using (var provider = Build())
        {
            var service = provider.GetRequiredService<CanonicalMemoryService>();
            sourceId = (await service.RememberAsync(Remember("source"))).MemoryId;
            promotion = new(sourceId, "promote", 1, "agent-b", "Keep useful parser behavior");
            provider.GetRequiredService<ManagedTransactionStore>().AfterMutation = index =>
            {
                if (index == 0)
                    throw new IOException("Simulated crash");
            };
            await Assert.ThrowsAsync<IOException>(() => service.PromoteAsync(promotion));
        }
        using var restarted = Build();
        var resumed = restarted.GetRequiredService<CanonicalMemoryService>();
        var promoted = await resumed.PromoteAsync(promotion);
        Assert.Equal(MemoryTier.Short, promoted.Tier);
        var target = (await resumed.GetAsync(new(promoted.MemoryId))).Record!;
        Assert.Contains(
            target.Relationships,
            relationship =>
                relationship.Kind == RelationshipKind.PromotedFrom
                && relationship.MemoryId == sourceId
        );
        Assert.Equal("agent-a", Assert.Single(target.Provenance).AgentId);
        _time.Advance(TimeSpan.FromDays(1));
        Assert.Null((await resumed.GetAsync(new(sourceId))).Record);
        Assert.NotNull((await resumed.GetAsync(new(promoted.MemoryId))).Record);
        Assert.Equal(promoted.MemoryId, (await resumed.PromoteAsync(promotion)).MemoryId);
    }

    [Fact]
    public async Task ShortPromotionCreatesDurablePendingCandidateWithoutDependencies()
    {
        string candidateId;
        using (var initial = Build())
        {
            var service = initial.GetRequiredService<CanonicalMemoryService>();
            var source = await service.RememberAsync(
                Remember("candidate-source", RememberTier.Short)
            );
            var promoted = await service.PromoteAsync(
                new(
                    source.MemoryId,
                    "candidate-promotion",
                    source.Revision,
                    "reviewer",
                    "Reusable across future sessions"
                )
            );
            candidateId = promoted.MemoryId;
            Assert.Equal(MemoryTier.Long, promoted.Tier);
            Assert.Equal(MemoryState.Candidate, promoted.Status);
        }

        using var restarted = Build();
        var candidate = (
            await restarted
                .GetRequiredService<CanonicalMemoryService>()
                .GetAsync(new(candidateId))
        ).Record!;
        Assert.Equal(MemoryState.Candidate, candidate.Status);
        Assert.Equal(EmbeddingState.PendingEmbedding, candidate.EmbeddingState);
        Assert.Equal(VectorIndexState.Pending, candidate.IndexState);
        Assert.Null(candidate.ValidatedAtUtc);
        Assert.Single(candidate.Provenance);
        Assert.Contains(
            candidate.Relationships,
            relationship => relationship.Kind == RelationshipKind.PromotedFrom
        );
    }

    [Fact]
    public async Task OutcomesAreReplaySafeAndUseIndependentConfirmationGrouping()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var candidate = await service.RememberAsync(
            Remember("outcome-candidate", RememberTier.LongCandidate)
        );
        RecordOutcomeRequest Outcome(
            string id,
            long revision,
            OutcomeKind result,
            string session = "session-one",
            string agent = "agent-one",
            string evidence = "evidence-one"
        ) =>
            new(
                candidate.MemoryId,
                id,
                revision,
                session,
                "task",
                agent,
                result,
                [evidence],
                "Observed result"
            );

        var first = Outcome("outcome-success-one", 1, OutcomeKind.Success);
        Assert.Equal(2, (await service.RecordOutcomeAsync(first)).Revision);
        Assert.Equal(2, (await service.RecordOutcomeAsync(first)).Revision);
        await service.RecordOutcomeAsync(
            Outcome("outcome-success-repeat", 2, OutcomeKind.Success, evidence: "evidence-two")
        );
        await service.RecordOutcomeAsync(
            Outcome("outcome-partial", 3, OutcomeKind.PartialSuccess)
        );
        await service.RecordOutcomeAsync(Outcome("outcome-failure", 4, OutcomeKind.Failure));
        await service.RecordOutcomeAsync(
            Outcome("outcome-contradicted", 5, OutcomeKind.Contradicted)
        );
        await service.RecordOutcomeAsync(
            Outcome("outcome-not-applicable", 6, OutcomeKind.NotApplicable)
        );
        await service.RecordOutcomeAsync(Outcome("outcome-stale", 7, OutcomeKind.Stale));
        await service.RecordOutcomeAsync(
            Outcome(
                "outcome-success-two",
                8,
                OutcomeKind.Success,
                "session-two",
                "agent-two",
                "evidence-three"
            )
        );

        var record = (await service.GetAsync(new(candidate.MemoryId))).Record!;
        Assert.Equal(9, record.Revision);
        Assert.Equal(3, record.SuccessCount);
        Assert.Equal(1, record.PartialSuccessCount);
        Assert.Equal(1, record.FailureCount);
        Assert.Equal(1, record.ContradictionCount);
        Assert.Equal(8, record.UseCount);
        Assert.Equal(2, record.IndependentConfirmations);
        Assert.Equal(0.5625, record.Confidence, 8);
        Assert.True(record.Contradicted);
        Assert.True(record.Stale);
        Assert.Equal(
            8,
            Directory.GetFiles(
                Path.Combine(
                    provider.GetRequiredService<MemoryStoragePaths>().LearningRoot,
                    "outcomes"
                ),
                "*.json",
                SearchOption.AllDirectories
            ).Length
        );
    }

    [Fact]
    public async Task CandidateValidationRequiresIndependentSuccessesAndHighRiskGrant()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var request = Remember("validation-candidate", RememberTier.LongCandidate);
        request.Content = "Reusable parser behavior";
        var candidate = await service.RememberAsync(request);
        async Task Outcome(
            string id,
            long revision,
            string session,
            string agent,
            string evidence
        ) =>
            await service.RecordOutcomeAsync(
                new(
                    candidate.MemoryId,
                    id,
                    revision,
                    session,
                    "task",
                    agent,
                    OutcomeKind.Success,
                    [evidence]
                )
            );
        await Outcome("confirm-one", 1, "session-one", "agent-one", "evidence-one");
        await Outcome("confirm-repeat", 2, "session-one", "agent-one", "evidence-two");
        var insufficient = await Assert.ThrowsAsync<ValidationException>(() =>
            service.PromoteAsync(
                new(candidate.MemoryId, "validate-early", 3, "reviewer", "Validate claim")
            )
        );
        Assert.Equal("validation_policy_failed", insufficient.Code);
        await Outcome("confirm-two", 3, "session-two", "agent-two", "evidence-three");
        var validated = await service.PromoteAsync(
            new(candidate.MemoryId, "validate", 4, "reviewer", "Validate claim")
        );
        Assert.Equal(MemoryState.Validated, validated.Status);
        var record = (await service.GetAsync(new(candidate.MemoryId))).Record!;
        Assert.Equal(2, record.IndependentConfirmations);
        Assert.NotNull(record.ValidatedAtUtc);
        Assert.Equal(VectorIndexState.Pending, record.IndexState);

        var riskyRequest = Remember("risky-candidate", RememberTier.LongCandidate);
        riskyRequest.Content = "Authentication policy observation";
        riskyRequest.Category = "security";
        var risky = await service.RememberAsync(riskyRequest);
        await service.RecordOutcomeAsync(
            new(
                risky.MemoryId,
                "risky-one",
                1,
                "risk-session-one",
                "task",
                "risk-agent-one",
                OutcomeKind.Success,
                ["risk-evidence-one"]
            )
        );
        await service.RecordOutcomeAsync(
            new(
                risky.MemoryId,
                "risky-two",
                2,
                "risk-session-two",
                "task",
                "risk-agent-two",
                OutcomeKind.Success,
                ["risk-evidence-two"]
            )
        );
        var riskyPromotion = new PromoteMemoryRequest(
            risky.MemoryId,
            "validate-risky",
            3,
            "reviewer",
            "Operator-reviewed validation"
        );
        var approval = await Assert.ThrowsAsync<ValidationException>(() =>
            service.PromoteAsync(riskyPromotion)
        );
        Assert.Equal("approval_required", approval.Code);
        var grantPath = provider
            .GetRequiredService<ManagedStoragePathResolver>()
            .ApprovalFile(risky.MemoryId, 3, "validate-learning");
        Directory.CreateDirectory(Path.GetDirectoryName(grantPath)!);
        await File.WriteAllTextAsync(
            grantPath,
            JsonSerializer.Serialize(
                new OperatorGrant(
                    1,
                    "repo",
                    risky.MemoryId,
                    3,
                    OperatorAction.ValidateLearning,
                    "operator",
                    _time.GetUtcNow().AddHours(1)
                ),
                MemoryJson.Options
            )
        );
        Assert.Equal(
            MemoryState.Validated,
            (await service.PromoteAsync(riskyPromotion)).Status
        );
    }

    [Fact]
    public async Task AuthoritativeValidationIsExplicitAndDisabledByDefault()
    {
        string id;
        using (var initial = Build())
        {
            var service = initial.GetRequiredService<CanonicalMemoryService>();
            id = (
                await service.RememberAsync(
                    Remember("authoritative-candidate", RememberTier.LongCandidate)
                )
            ).MemoryId;
            var disabled = await Assert.ThrowsAsync<ValidationException>(() =>
                service.PromoteAsync(
                    new(
                        id,
                        "authoritative-disabled",
                        1,
                        "reviewer",
                        "Reproducible repository fact",
                        true,
                        ["test-command-output"]
                    )
                )
            );
            Assert.Equal("validation_policy_failed", disabled.Code);
        }
        using var enabled = Build(allowAuthoritativeVerification: true);
        var result = await enabled
            .GetRequiredService<CanonicalMemoryService>()
            .PromoteAsync(
                new(
                    id,
                    "authoritative-enabled",
                    1,
                    "reviewer",
                    "Reproducible repository fact",
                    true,
                    ["test-command-output"]
                )
            );
        Assert.Equal(MemoryState.Validated, result.Status);
    }

    [Fact]
    public async Task SupersessionAndRetirementPreserveHistoryAndExitOrdinaryRecall()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var oldRequest = Remember("old-learning", RememberTier.LongCandidate);
        oldRequest.Content = "Parser requires legacy normalization";
        var old = await service.RememberAsync(oldRequest);
        var replacementRequest = Remember("new-learning", RememberTier.LongCandidate);
        replacementRequest.Content = "Parser uses current Unicode normalization";
        var replacement = await service.RememberAsync(replacementRequest);

        var superseded = await service.SupersedeAsync(
            new(
                old.MemoryId,
                "supersede-learning",
                1,
                "reviewer",
                "Current implementation replaced the old behavior",
                ["parser-test"],
                replacement.MemoryId
            )
        );
        Assert.Equal(MemoryState.Superseded, superseded.Status);
        Assert.Null((await service.GetAsync(new(old.MemoryId))).Record);
        var history = (
            await service.GetAsync(new(old.MemoryId, IncludeHistory: true))
        ).Record!;
        Assert.Contains(
            history.Relationships,
            relationship =>
                relationship.Kind == RelationshipKind.SupersededBy
                && relationship.MemoryId == replacement.MemoryId
        );
        var current = (await service.GetAsync(new(replacement.MemoryId))).Record!;
        Assert.Contains(
            current.Relationships,
            relationship =>
                relationship.Kind == RelationshipKind.Supersedes
                && relationship.MemoryId == old.MemoryId
        );
        var recall = await service.RecallAsync(
            new() { Query = "Parser", IncludeCandidates = true }
        );
        Assert.DoesNotContain(recall.Memories, item => item.Record.Id == old.MemoryId);
        Assert.Contains(recall.Memories, item => item.Record.Id == replacement.MemoryId);

        var retired = await service.RetireAsync(
            new(
                replacement.MemoryId,
                "retire-learning",
                current.Revision,
                "reviewer",
                "Repository behavior changed again",
                ["new-parser-test"]
            )
        );
        Assert.Equal(MemoryState.Retired, retired.Status);
        Assert.Null((await service.GetAsync(new(replacement.MemoryId))).Record);
        Assert.Equal(
            MemoryState.Retired,
            (await service.GetAsync(new(replacement.MemoryId, IncludeHistory: true)))
                .Record!
                .Status
        );
        Assert.Empty(
            (
                await service.RecallAsync(
                    new() { Query = "Parser", IncludeCandidates = true }
                )
            ).Memories
        );
    }

    [Fact]
    public async Task ProtectedDeletionRequiresExactOperatorGrantAndAuditHasNoContent()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var request = Remember("protected", RememberTier.Short);
        request.Protected = true;
        var saved = await service.RememberAsync(request);
        var deletion = new DeleteMemoryRequest(
            saved.MemoryId,
            "delete",
            1,
            "agent-a",
            "Obsolete observation"
        );
        var denied = await Assert.ThrowsAsync<ValidationException>(() =>
            service.DeleteAsync(deletion)
        );
        Assert.Equal("approval_required", denied.Code);
        var grantPath = provider
            .GetRequiredService<ManagedStoragePathResolver>()
            .ApprovalFile(saved.MemoryId, 1, "delete-protected-memory");
        Directory.CreateDirectory(Path.GetDirectoryName(grantPath)!);
        var grant = new OperatorGrant(
            1,
            "other-repo",
            saved.MemoryId,
            1,
            OperatorAction.DeleteProtectedMemory,
            "local-operator",
            _time.GetUtcNow().AddHours(1)
        );
        await File.WriteAllTextAsync(
            grantPath,
            JsonSerializer.Serialize(grant, MemoryJson.Options)
        );
        await Assert.ThrowsAsync<ValidationException>(() => service.DeleteAsync(deletion));
        grant = grant with { RepositoryId = "repo" };
        await File.WriteAllTextAsync(
            grantPath,
            JsonSerializer.Serialize(grant, MemoryJson.Options)
        );
        Assert.Equal(MemoryState.Deleted, (await service.DeleteAsync(deletion)).Status);
        Assert.Null((await service.GetAsync(new(saved.MemoryId))).Record);
        var eventFile = Assert.Single(
            Directory.GetFiles(_root, "events.jsonl", SearchOption.AllDirectories)
        );
        var audit = await File.ReadAllTextAsync(eventFile);
        Assert.DoesNotContain(request.Content, audit);
        Assert.Contains(deletion.Reason, audit);
        Assert.Contains("\"eventType\":\"memory-deleted\"", audit);
        await service.DeleteAsync(deletion);
        Assert.Single(
            await File.ReadAllLinesAsync(eventFile),
            line => line.Contains("\"eventId\":\"delete\"", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task EventJournalRepairsPartialTailRotatesAndDoesNotDuplicateReplay()
    {
        using var provider = Build(journalBytes: 512);
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var first = Remember("event-one", RememberTier.Short);
        first.Content = "First journal claim";
        var saved = await service.RememberAsync(first);
        var live = Assert.Single(
            Directory.GetFiles(_root, "events.jsonl", SearchOption.AllDirectories)
        );
        await File.AppendAllTextAsync(live, "{\"schemaVersion\":1");

        for (var index = 2; index <= 6; index++)
        {
            var next = Remember($"event-{index}", RememberTier.Short);
            next.Content = $"Distinct journal claim {index}";
            await service.RememberAsync(next);
        }
        Assert.Equal(saved.MemoryId, (await service.RememberAsync(first)).MemoryId);

        var files = Directory.GetFiles(
            Path.GetDirectoryName(live)!,
            "events*.jsonl",
            SearchOption.TopDirectoryOnly
        );
        Assert.True(files.Length > 1);
        var lines = new List<string>();
        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file);
            Assert.DoesNotContain("{\"schemaVersion\":1{", content);
            lines.AddRange(content.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }
        Assert.Equal(6, lines.Count);
        Assert.Equal(6, lines.Select(line =>
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.GetProperty("eventId").GetString();
        }).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task RecallOrdersTiersFiltersMetadataAndNeverReturnsExpiredTemp()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var temp = await service.RememberAsync(Remember("temp"));
        await service.RememberAsync(Remember("short", RememberTier.Short));
        await service.RememberAsync(Remember("long", RememberTier.LongCandidate));
        var ordinary = await service.RecallAsync(new() { Query = "Unicode", AgentId = "agent-a" });
        Assert.Equal(
            new[] { MemoryTier.Temp, MemoryTier.Short },
            ordinary.Memories.Select(hit => hit.Record.Tier)
        );
        Assert.All(ordinary.Memories, hit => Assert.Equal("lexical", hit.MatchType));
        Assert.Empty((await service.RecallAsync(new() { SessionId = "another-session" })).Memories);
        _time.Advance(TimeSpan.FromDays(1));
        var recent = await service.RecallAsync(new() { IncludeCandidates = true });
        Assert.DoesNotContain(recent.Memories, hit => hit.Record.Id == temp.MemoryId);
        Assert.All(recent.Memories, hit => Assert.Null(hit.Score));
    }

    [Fact]
    public async Task ReviewAndAgeNeverDeleteUnreviewedShortKnowledge()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var saved = await service.RememberAsync(Remember("short", RememberTier.Short));
        Assert.Empty(await service.ListReviewAsync(new()));
        _time.Advance(TimeSpan.FromDays(14));
        Assert.Single(await service.ListReviewAsync(new()));
        await service.ReviewAsync(
            new(
                saved.MemoryId,
                "review",
                1,
                "reviewer",
                ReviewState.DeleteCandidate,
                "Needs explicit decision",
                ["review-note"]
            )
        );
        Assert.NotNull((await service.GetAsync(new(saved.MemoryId))).Record);
        await service.CleanupAsync();
        Assert.NotNull((await service.GetAsync(new(saved.MemoryId))).Record);
    }

    [Theory]
    [InlineData(ReviewState.Keep)]
    [InlineData(ReviewState.CompactCandidate)]
    [InlineData(ReviewState.PromotionCandidate)]
    [InlineData(ReviewState.ArchiveCandidate)]
    [InlineData(ReviewState.DeleteCandidate)]
    public async Task EveryShortReviewActionPersistsAcrossRestartAndRemainsFilterable(
        ReviewState action
    )
    {
        var operation = action.ToString().ToLowerInvariant();
        string id;
        using (var initial = Build())
        {
            var service = initial.GetRequiredService<CanonicalMemoryService>();
            var request = Remember("create-" + operation, RememberTier.Short);
            request.Content = "Review action " + operation;
            request.DecisionArea = "review-flow";
            id = (await service.RememberAsync(request)).MemoryId;
            var reviewed = await service.ReviewAsync(
                new(
                    id,
                    "review-" + operation,
                    1,
                    "reviewer",
                    action,
                    "Explicit review decision",
                    ["evidence-reference"]
                )
            );
            Assert.Equal(2, reviewed.Revision);
        }

        using var restarted = Build();
        var resumed = restarted.GetRequiredService<CanonicalMemoryService>();
        var record = (await resumed.GetAsync(new(id))).Record!;
        Assert.Equal(action, record.ReviewState);
        Assert.Equal("reviewer", record.AgentId);
        Assert.Equal("Explicit review decision", record.ReviewReason);
        Assert.Equal(["evidence-reference"], record.ReviewEvidence);
        var filtered = await resumed.ListReviewAsync(
            new(
                ReviewState: action,
                Category: "observation",
                DecisionArea: "review-flow",
                AgentId: "reviewer",
                TaskId: "task"
            )
        );
        Assert.Equal(id, Assert.Single(filtered).Record.Id);
    }

    [Fact]
    public async Task ArchiveRecoversFromInterruptedStageAndSupportsExplicitLookup()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var firstRequest = Remember("archive-source-one", RememberTier.Short);
        firstRequest.Content = "First archived observation";
        var secondRequest = Remember("archive-source-two", RememberTier.Short);
        secondRequest.Content = "Second archived observation";
        var first = await service.RememberAsync(firstRequest);
        var second = await service.RememberAsync(secondRequest);
        var request = new ArchiveMemoryRequest(
            "archive-operation",
            "archiver",
            "Reviewed records are no longer active",
            [new(first.MemoryId, 1), new(second.MemoryId, 1)]
        );
        var transaction = provider.GetRequiredService<ManagedTransactionStore>();
        transaction.AfterMutation = index =>
        {
            if (index == 0)
                throw new IOException("Simulated archive staging crash");
        };
        await Assert.ThrowsAsync<IOException>(() => service.ArchiveAsync(request));
        transaction.AfterMutation = null;

        var archived = await service.ArchiveAsync(request);
        Assert.Equal(2, archived.ArchivedCount);
        Assert.Single(Directory.GetFiles(_root, "*.jsonl.gz", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(_root, first.MemoryId + ".json", SearchOption.AllDirectories));
        Assert.Null((await service.GetAsync(new(first.MemoryId))).Record);
        var restored = await service.GetAsync(new(first.MemoryId, IncludeArchived: true));
        Assert.Equal("archived", restored.Availability);
        Assert.Equal(firstRequest.Content, restored.Record!.Content);
        var archivedRecall = await service.RecallAsync(
            new()
            {
                Query = "archived observation",
                Tiers = [MemoryTier.Short],
                MemoryIds = [first.MemoryId],
                IncludeArchived = true,
            }
        );
        Assert.Equal(first.MemoryId, Assert.Single(archivedRecall.Memories).Record.Id);
        var replay = await service.ArchiveAsync(request);
        Assert.Equal(archived.ArchiveId, replay.ArchiveId);
        Assert.Equal(archived.MemoryIds, replay.MemoryIds);
        Assert.Equal(archived.ArchivedCount, replay.ArchivedCount);
    }

    [Fact]
    public async Task CorruptArchiveVerificationNeverRemovesSources()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var saved = await service.RememberAsync(Remember("corrupt-source", RememberTier.Short));
        var transaction = provider.GetRequiredService<ManagedTransactionStore>();
        var corrupted = false;
        transaction.AfterDurableBoundary = boundary =>
        {
            if (corrupted || boundary != "receipt")
                return;
            var archive = Directory
                .GetFiles(_root, "*.jsonl.gz", SearchOption.AllDirectories)
                .SingleOrDefault();
            if (archive is null)
                return;
            corrupted = true;
            File.WriteAllBytes(archive, [1, 2, 3, 4]);
        };

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ArchiveAsync(
                new(
                    "corrupt-archive",
                    "archiver",
                    "Verify before removal",
                    [new(saved.MemoryId, 1)]
                )
            )
        );
        Assert.Equal("storage_invalid", error.Code);
        transaction.AfterDurableBoundary = null;
        Assert.NotNull((await service.GetAsync(new(saved.MemoryId))).Record);
        Assert.Single(Directory.GetFiles(_root, saved.MemoryId + ".json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ArchivedLookupEnforcesConfiguredDecompressionLimit()
    {
        string id;
        using (var initial = Build())
        {
            var service = initial.GetRequiredService<CanonicalMemoryService>();
            var items = new List<ArchiveMemoryItem>();
            for (var index = 0; index < 4; index++)
            {
                var request = Remember($"large-archive-{index}", RememberTier.Short);
                request.Content = new string((char)('a' + index), 1500);
                var saved = await service.RememberAsync(request);
                items.Add(new(saved.MemoryId, saved.Revision));
            }
            id = items[0].MemoryId;
            await service.ArchiveAsync(
                new("large-archive", "archiver", "Bounded archive read test", items)
            );
        }

        using var bounded = Build(
            journalBytes: 512,
            maxContentBytes: 2_048,
            maxRecordBytes: 4_096,
            archiveCompressedBytes: 4_096,
            archiveExpandedBytes: 4_096
        );
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            bounded
                .GetRequiredService<CanonicalMemoryService>()
                .GetAsync(new(id, IncludeArchived: true))
        );
        Assert.Equal("capacity_exceeded", error.Code);
    }

    [Fact]
    public async Task CompactionPreservesCoverageArchivesSourcesAndReplaysAfterCommitInterruption()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var firstRequest = Remember("compact-first", RememberTier.Short);
        firstRequest.Content = "Parser accepts Unicode identifiers.";
        var secondRequest = Remember("compact-second", RememberTier.Short);
        secondRequest.Content = "Parser rejects malformed surrogate pairs.";
        var first = await service.RememberAsync(firstRequest);
        var second = await service.RememberAsync(secondRequest);
        await service.ReviewAsync(
            new(first.MemoryId, "review-compact-first", 1, "reviewer", ReviewState.CompactCandidate, "Related parser evidence", ["test:unicode"])
        );
        await service.ReviewAsync(
            new(second.MemoryId, "review-compact-second", 1, "reviewer", ReviewState.CompactCandidate, "Related parser evidence", ["test:surrogate"])
        );
        var sources = new List<ArchiveMemoryItem> { new(first.MemoryId, 2), new(second.MemoryId, 2) };
        var prepared = await service.PrepareCompactionAsync(
            new("prepare-parser", "reviewer", sources)
        );
        Assert.Equal(2, prepared.Sources.Count);
        var request = new CommitCompactionRequest(
            "commit-parser",
            prepared.PreparationToken,
            "reviewer",
            "Reviewed parser findings",
            "Parser Unicode behavior",
            "The parser accepts valid Unicode and rejects malformed surrogate pairs.",
            ["Valid Unicode identifiers are accepted."],
            [],
            [],
            ["Unicode parser tests pass."],
            [],
            ["test:unicode", "test:surrogate"],
            sources,
            [
                new(first.MemoryId, ["test:unicode"], []),
                new(second.MemoryId, ["test:surrogate"], []),
            ],
            ["parser", "unicode"],
            "parsing",
            "session",
            "task"
        );
        var transaction = provider.GetRequiredService<ManagedTransactionStore>();
        var receipts = 0;
        transaction.AfterDurableBoundary = boundary =>
        {
            if (boundary == "receipt" && ++receipts == 2)
                throw new IOException("Simulated response loss after compaction commit");
        };
        await Assert.ThrowsAsync<IOException>(() => service.CommitCompactionAsync(request));
        transaction.AfterDurableBoundary = null;

        var replay = await service.CommitCompactionAsync(request);
        var compacted = (await service.GetAsync(new(replay.MemoryId))).Record!;
        Assert.Equal(MemoryState.Compacted, compacted.Status);
        Assert.Equal(ReviewState.Reviewed, compacted.ReviewState);
        Assert.Equal(2, compacted.Compaction!.SourceCoverage.Count);
        Assert.All(sources, source =>
            Assert.Contains(
                compacted.Relationships,
                relationship => relationship.Kind == RelationshipKind.CompactedFrom
                    && relationship.MemoryId == source.MemoryId
            )
        );
        Assert.Equal("archived", (await service.GetAsync(new(first.MemoryId, IncludeArchived: true))).Availability);
        Assert.Equal("archived", (await service.GetAsync(new(second.MemoryId, IncludeArchived: true))).Availability);
        Assert.Equal(replay.SourceMemoryIds, replay.ArchivedMemoryIds);
        Assert.Single(Directory.GetFiles(_root, "*.jsonl.gz", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CompactionRejectsStaleOrIncompleteSourceCoverageWithoutRemovingSources()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var firstRequest = Remember("coverage-first", RememberTier.Short);
        firstRequest.Content = "First coverage source.";
        var secondRequest = Remember("coverage-second", RememberTier.Short);
        secondRequest.Content = "Second coverage source.";
        var first = await service.RememberAsync(firstRequest);
        var second = await service.RememberAsync(secondRequest);
        await service.ReviewAsync(
            new(first.MemoryId, "review-coverage-first", 1, "reviewer", ReviewState.CompactCandidate, "review", ["evidence:first"])
        );
        await service.ReviewAsync(
            new(second.MemoryId, "review-coverage-second", 1, "reviewer", ReviewState.CompactCandidate, "review", ["evidence:second"])
        );
        var sources = new List<ArchiveMemoryItem> { new(first.MemoryId, 2), new(second.MemoryId, 2) };
        var prepared = await service.PrepareCompactionAsync(new("prepare-coverage", "reviewer", sources));
        var incomplete = new CommitCompactionRequest(
            "commit-incomplete",
            prepared.PreparationToken,
            "reviewer",
            "reviewed",
            "Summary",
            "Summary content",
            [], [], [], [], [],
            ["evidence:first"],
            sources,
            [new(first.MemoryId, ["evidence:first"], [])],
            []
        );
        var coverageError = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CommitCompactionAsync(incomplete)
        );
        Assert.Equal("source_coverage_incomplete", coverageError.Code);

        await service.ReviewAsync(
            new(first.MemoryId, "review-again", 2, "reviewer", ReviewState.CompactCandidate, "new evidence", ["evidence:new"])
        );
        var stale = incomplete with
        {
            OperationId = "commit-stale",
            Evidence = ["evidence:first", "evidence:second"],
            SourceCoverage =
            [
                new(first.MemoryId, ["evidence:first"], []),
                new(second.MemoryId, ["evidence:second"], []),
            ],
        };
        var staleError = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CommitCompactionAsync(stale)
        );
        Assert.Equal("revision_conflict", staleError.Code);
        Assert.NotNull((await service.GetAsync(new(first.MemoryId))).Record);
        Assert.NotNull((await service.GetAsync(new(second.MemoryId))).Record);
    }

    [Fact]
    public async Task ReviewedDuplicateMergeUsesCompactionAndExplicitRelationships()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var firstRequest = Remember("duplicate-merge-first", RememberTier.Short);
        firstRequest.Content = "First independently observed cache rule.";
        var secondRequest = Remember("duplicate-merge-second", RememberTier.Short);
        secondRequest.Content = "Second independently observed cache rule.";
        var first = await service.RememberAsync(firstRequest);
        var second = await service.RememberAsync(secondRequest);
        await service.ReviewAsync(new(first.MemoryId, "duplicate-review-first", 1, "reviewer", ReviewState.CompactCandidate, "Reviewed as duplicate", ["evidence:first"]));
        await service.ReviewAsync(new(second.MemoryId, "duplicate-review-second", 1, "reviewer", ReviewState.CompactCandidate, "Reviewed as duplicate", ["evidence:second"]));
        var sources = new List<ArchiveMemoryItem> { new(first.MemoryId, 2), new(second.MemoryId, 2) };
        var result = await service.CompactAsync(
            new(
                "merge-duplicates",
                "reviewer",
                "Explicit duplicate review",
                "Cache rule",
                "Both observations describe the reviewed cache rule.",
                ["The rule was observed twice."],
                [], [], [], [],
                ["evidence:first", "evidence:second"],
                sources,
                [
                    new(first.MemoryId, ["evidence:first"], []),
                    new(second.MemoryId, ["evidence:second"], []),
                ],
                ["cache"],
                Kind: CompactionKind.ReviewedDuplicateMerge
            )
        );
        var merged = (await service.GetAsync(new(result.MemoryId))).Record!;
        Assert.Equal(CompactionKind.ReviewedDuplicateMerge, merged.Compaction!.Kind);
        Assert.Equal(
            2,
            merged.Relationships.Count(relationship => relationship.Kind == RelationshipKind.DuplicateOf)
        );
    }

    [Fact]
    public async Task MaintenanceSuggestsByAgeAndOnlyProcessesExplicitArchiveCandidates()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var unreviewedRequest = Remember("old-unreviewed", RememberTier.Short);
        unreviewedRequest.Content = "Old knowledge still needs human review.";
        var approvedRequest = Remember("approved-archive", RememberTier.Short);
        approvedRequest.Content = "Reviewed knowledge may be archived.";
        var unreviewed = await service.RememberAsync(unreviewedRequest);
        var approved = await service.RememberAsync(approvedRequest);
        _time.Advance(TimeSpan.FromDays(14));

        var suggestions = await service.ListReviewAsync(new(MaxResults: 10));
        var unreviewedSuggestion = Assert.Single(
            suggestions,
            candidate => candidate.Record.Id == unreviewed.MemoryId
        );
        Assert.Equal(ReviewState.ArchiveCandidate, unreviewedSuggestion.SuggestedAction);
        Assert.False(unreviewedSuggestion.ExplicitlyApproved);

        await service.ReviewAsync(
            new(
                approved.MemoryId,
                "approve-archive",
                1,
                "reviewer",
                ReviewState.ArchiveCandidate,
                "No longer needed in active review",
                ["review:archive"]
            )
        );
        var result = await service.RunMaintenanceAsync();
        Assert.Equal(1, result.Archived);
        Assert.NotNull((await service.GetAsync(new(unreviewed.MemoryId))).Record);
        Assert.Null((await service.GetAsync(new(approved.MemoryId))).Record);
        Assert.Equal(
            "archived",
            (await service.GetAsync(new(approved.MemoryId, IncludeArchived: true))).Availability
        );
    }

    [Fact]
    public async Task ObservationEventsAreReplaySafeAndCannotSpoofLifecycleTransitions()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var request = new RecordMemoryEventRequest(
            "observation-one",
            "observation-tool-result",
            "agent-a",
            SessionId: "session",
            TaskId: "task",
            Metadata: new() { ["result"] = "passed" }
        );
        var first = await service.RecordObservationEventAsync(request);
        var replay = await service.RecordObservationEventAsync(request);
        Assert.Equal(first.TimestampUtc, replay.TimestampUtc);
        var journal = Assert.Single(
            Directory.GetFiles(_root, "events.jsonl", SearchOption.AllDirectories)
        );
        Assert.Single(await File.ReadAllLinesAsync(journal), line => line.Length > 0);

        var reserved = await Assert.ThrowsAsync<ValidationException>(() =>
            service.RecordObservationEventAsync(request with
            {
                EventId = "spoofed-event",
                EventType = "memory-validated",
            })
        );
        Assert.Equal("event_type_reserved", reserved.Code);
        var conflict = await Assert.ThrowsAsync<ValidationException>(() =>
            service.RecordObservationEventAsync(request with
            {
                Metadata = new() { ["result"] = "different" },
            })
        );
        Assert.Equal("operation_conflict", conflict.Code);
    }

    [Fact]
    public async Task EncodedSecretsAreRejectedBeforeCanonicalEventOrEmbeddingWorkIsPersisted()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        const string decoded = "api_key=synthetic-secret-value";
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(decoded));
        var remember = Remember("secret-record", RememberTier.LongCandidate);
        remember.Content = encoded;
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            service.RememberAsync(remember)
        );
        Assert.Equal("content_policy_rejected", error.Code);
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.RecordObservationEventAsync(
                new(
                    "secret-event",
                    "observation-security-test",
                    "agent-a",
                    Metadata: new() { ["evidence"] = encoded }
                )
            )
        );
        var files = Directory.Exists(_root)
            ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            : [];
        foreach (var file in files)
        {
            if (new FileInfo(file).Length > 1_048_576)
                continue;
            var bytes = await File.ReadAllBytesAsync(file);
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain(encoded, text);
            Assert.DoesNotContain(decoded, text);
        }
        Assert.Empty(
            Directory.Exists(_root)
                ? Directory.GetFiles(_root, "*.json", SearchOption.AllDirectories)
                : []
        );
    }

    [Fact]
    public async Task StatusReportsCanonicalCountsAndFilesystemReindexIsBounded()
    {
        var activities = new ThreadSafeCollection<Activity>();
        using var tracing = Sdk.CreateTracerProviderBuilder()
            .AddSource(TelemetrySchema.ActivitySourceName)
            .AddInMemoryExporter(activities)
            .Build();
        using var provider = Build();
        var memory = provider.GetRequiredService<CanonicalMemoryService>();
        await memory.RememberAsync(Remember("status-temp"));
        var shortRequest = Remember("status-short", RememberTier.Short);
        shortRequest.Content = "Status short record.";
        await memory.RememberAsync(shortRequest);
        var longRequest = Remember("status-long", RememberTier.LongCandidate);
        longRequest.Content = "Status long candidate.";
        await memory.RememberAsync(longRequest);

        var status = await provider.GetRequiredService<MemoryStatusService>().GetAsync();
        Assert.Equal("repo", status.RepositoryId);
        Assert.Equal(_root, status.SystemRoot);
        Assert.Equal(1, status.TempRecords);
        Assert.Equal(1, status.ShortActiveRecords);
        Assert.Equal(1, status.LongCandidateRecords);
        Assert.Equal(1, status.PendingEmbeddingRecords);
        Assert.Equal("degraded", status.Qdrant.State);
        Assert.Equal("semantic_active_collection_missing", status.Qdrant.ErrorCode);

        var reindexed = await provider.GetRequiredService<MemoryReindexService>().ReindexAsync(
            new("filesystem-reindex", MemoryReindexScope.FileSystem, MaxItems: 10)
        );
        Assert.Equal(MemoryReindexScope.FileSystem, reindexed.Scope);
        Assert.Equal(0, reindexed.InvalidCanonicalRecords);
        Assert.False(reindexed.ModelMigrated);
        Assert.True(tracing.ForceFlush(5_000));
        var reindex = Assert.Single(activities, activity =>
            activity.OperationName == "mcp operation maintenance reindex");
        Assert.Equal(ActivityKind.Internal, reindex.Kind);
        Assert.Equal("maintenance", reindex.GetTagItem("dependency.type"));
        Assert.Equal("reindex", reindex.GetTagItem("dependency.operation"));
        Assert.Equal(10, reindex.GetTagItem("mcp.batch.items"));
        Assert.Equal("success", reindex.GetTagItem("mcp.status"));
        var tags = string.Join("|", reindex.TagObjects.Select(tag => tag.Value));
        Assert.DoesNotContain("filesystem-reindex", tags, StringComparison.Ordinal);
        Assert.DoesNotContain(_root, tags, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShorterConfiguredLifetimeAppliesAfterRestart()
    {
        string id;
        using (var initial = Build())
            id = (
                await initial
                    .GetRequiredService<CanonicalMemoryService>()
                    .RememberAsync(Remember("temp"))
            ).MemoryId;
        _time.Advance(TimeSpan.FromHours(1));
        using var stricter = Build(0.5);
        Assert.Null(
            (await stricter.GetRequiredService<CanonicalMemoryService>().GetAsync(new(id))).Record
        );
        Assert.Empty(Directory.GetFiles(_root, id + ".json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ServerStartupPhysicallySweepsExpiredTempWithoutGetCall()
    {
        _time.Set(DateTimeOffset.UtcNow.AddHours(-25));
        string id;
        using (var seed = Build())
            id = (
                await seed.GetRequiredService<CanonicalMemoryService>()
                    .RememberAsync(Remember("seed"))
            ).MemoryId;
        var file = Assert.Single(
            Directory.GetFiles(_root, id + ".json", SearchOption.AllDirectories)
        );
        using var server = new McpProcess(_root);
        await server.InitializeAsync();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (File.Exists(file) && DateTime.UtcNow < deadline)
            await Task.Delay(25);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task FutureDatedCanonicalRecordIsExcludedWithoutDeletion()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<CanonicalMemoryService>();
        var saved = await service.RememberAsync(Remember("future-record"));
        var file = Assert.Single(
            Directory.GetFiles(_root, saved.MemoryId + ".json", SearchOption.AllDirectories)
        );
        var record = JsonSerializer.Deserialize<MemoryRecord>(
            await File.ReadAllTextAsync(file),
            MemoryJson.Options
        )!;
        record.CreatedAtUtc = _time.GetUtcNow().AddMinutes(1);
        record.UpdatedAtUtc = record.CreatedAtUtc;
        record.ExpiresAtUtc = record.CreatedAtUtc.AddHours(1);
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(record, MemoryJson.Options));

        Assert.Equal(1, await provider.GetRequiredService<LearningCatalog>().RebuildAsync());
        Assert.Null((await service.GetAsync(new(saved.MemoryId))).Record);
        Assert.True(File.Exists(file));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan value) => _now += value;

        public void Set(DateTimeOffset value) => _now = value;
    }
}
