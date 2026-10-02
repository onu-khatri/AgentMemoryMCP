using System.Diagnostics;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Extensions;
using AgentSession.MCP.Models.Memory;
using AgentSession.MCP.Observability;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AgentSession.MCP.Tests;

public sealed class QdrantVectorIndexTests
{
    [Fact]
    public void PointIdsAreStableAndRepositoryScoped()
    {
        var first = QdrantVectorIndex.PointId("repo-one", "memory-one");

        Assert.Equal(first, QdrantVectorIndex.PointId("repo-one", "memory-one"));
        Assert.NotEqual(first, QdrantVectorIndex.PointId("repo-two", "memory-one"));
        Assert.NotEqual(first, QdrantVectorIndex.PointId("repo-one", "memory-two"));
        Assert.Equal(5, first.Version);
        Assert.Equal(8, first.Variant);
    }

    [Fact]
    public async Task LogicalOperationsEmitBoundedSpansAndFailureMetricsWithFakeBackendCalls()
    {
        var activities = new ThreadSafeCollection<Activity>();
        var failures = new TestDependencyFailureRecorder();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(TelemetrySchema.ActivitySourceName)
            .AddInMemoryExporter(activities)
            .Build();
        var index = new QdrantVectorIndex(
            new QdrantClient("localhost", 6334),
            new MemoryContentPolicy(),
            new McpDependencyTelemetry(failures)
        );

        foreach (var operation in new[] { "health", "migration", "query", "delete" })
            await index.TrackOperationAsync(
                "qdrant",
                operation,
                operation is "query" or "delete" ? 7 : null,
                _ => Task.CompletedTask,
                CancellationToken.None
            );
        foreach (var operation in new[] { "collection", "upsert" })
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                index.TrackOperationAsync(
                    "qdrant",
                    operation,
                    operation == "upsert" ? 1 : null,
                    _ => throw new InvalidOperationException(
                        "private-qdrant.example/collection/repository/memory/point/vector/filter/payload"
                    ),
                    CancellationToken.None
                )
            );
        Assert.True(provider.ForceFlush(5_000));

        var qdrantActivities = activities.Where(activity =>
            Equals(activity.GetTagItem("dependency.type"), "qdrant")).ToArray();
        Assert.Equal(6, qdrantActivities.Length);
        Assert.Equal(
            new[] { "collection", "delete", "health", "migration", "query", "upsert" },
            qdrantActivities
                .Select(activity => activity.GetTagItem("dependency.operation")?.ToString())
                .Order(StringComparer.Ordinal)
        );
        Assert.All(qdrantActivities, activity =>
        {
            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal("qdrant", activity.GetTagItem("dependency.type"));
            Assert.Empty(activity.Events);
        });
        Assert.All(
            qdrantActivities.Where(activity =>
                activity.GetTagItem("dependency.operation")?.ToString()
                    is "collection" or "upsert"),
            activity =>
            {
                Assert.Equal("dependency_error", activity.GetTagItem("mcp.status"));
                Assert.Equal("dependency_grpc_error", activity.GetTagItem("error.type"));
                Assert.Equal(ActivityStatusCode.Error, activity.Status);
            }
        );
        Assert.All(
            qdrantActivities.Where(activity =>
                activity.GetTagItem("dependency.operation")?.ToString()
                    is not ("collection" or "upsert")),
            activity => Assert.Equal("success", activity.GetTagItem("mcp.status"))
        );
        Assert.Equal(
            7,
            qdrantActivities.Single(activity =>
                Equals(activity.GetTagItem("dependency.operation"), "query"))
                .GetTagItem("mcp.batch.items")
        );
        Assert.Equal(
            1,
            qdrantActivities.Single(activity =>
                Equals(activity.GetTagItem("dependency.operation"), "upsert"))
                .GetTagItem("mcp.batch.items")
        );
        var expectedFailures = failures.Failures.Where(failure =>
            failure.Type == "qdrant"
            && failure.Operation is "collection" or "upsert"
        ).ToArray();
        Assert.Equal(2, expectedFailures.Length);
        Assert.All(expectedFailures, failure =>
        {
            Assert.Equal("qdrant", failure.Type);
            Assert.Contains(failure.Operation, new[] { "collection", "upsert" });
        });
        var exported = string.Join(
            "|",
            qdrantActivities.SelectMany(activity => activity.TagObjects).Select(tag => tag.Value)
        );
        foreach (var forbidden in new[]
        {
            "private-qdrant",
            "collection/repository",
            "memory",
            "point",
            "vector",
            "filter",
            "payload",
        })
            Assert.DoesNotContain(forbidden, exported, StringComparison.OrdinalIgnoreCase);
    }

    [QdrantFact]
    [Trait("Category", "QdrantIntegration")]
    public async Task RealQdrantEnforcesRevisionAndMetadataFilters()
    {
        using var client = new QdrantClient("localhost", 6334);
        var repositoryId = "integration-repo";
        var fingerprint = EmbeddingProjection.Fingerprint(
            "ollama",
            "embeddinggemma",
            "integration-digest",
            8
        );
        var prefix = "agent_memory_test_" + Guid.NewGuid().ToString("N");
        var identity = EmbeddingProjection.Collection(prefix, repositoryId, fingerprint);
        var index = new QdrantVectorIndex(
            client,
            new MemoryContentPolicy(),
            new McpDependencyTelemetry(new TestDependencyFailureRecorder())
        );
        var vector = new[] { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };

        try
        {
            await index.EnsureCollectionAsync(identity);
            var info = await client.GetCollectionInfoAsync(identity.PhysicalCollection);
            foreach (
                var field in new[]
                {
                    "repositoryId",
                    "status",
                    "category",
                    "tags",
                    "revision",
                    "embeddingFingerprint",
                }
            )
                Assert.True(info.PayloadSchema.ContainsKey(field), $"Missing index: {field}");

            var architecture = Record(
                "memory-one",
                repositoryId,
                fingerprint,
                MemoryState.Validated,
                "architecture",
                ["caching"]
            );
            architecture.DecisionArea = "storage";
            var candidate = Record(
                "memory-two",
                repositoryId,
                fingerprint,
                MemoryState.Candidate,
                "architecture",
                ["database"]
            );
            var operations = Record(
                "memory-three",
                repositoryId,
                fingerprint,
                MemoryState.Validated,
                "operations",
                ["caching"]
            );
            await index.UpsertAsync(identity, architecture, vector);
            await index.UpsertAsync(identity, candidate, vector);
            await index.UpsertAsync(identity, operations, vector);
            await index.UpsertAsync(identity, architecture, vector); // idempotent replay

            await client.UpsertAsync(
                identity.PhysicalCollection,
                [
                    new PointStruct
                    {
                        Id = Guid.NewGuid(),
                        Vectors = vector,
                        Payload =
                        {
                            ["memoryId"] = "foreign-memory",
                            ["repositoryId"] = "other-repo",
                            ["status"] = "validated",
                            ["category"] = "architecture",
                            ["tags"] = new[] { "caching" },
                            ["contentHash"] = "foreign-hash",
                            ["embeddingFingerprint"] = fingerprint.Hash,
                            ["revision"] = 1L,
                        },
                    },
                ],
                wait: true
            );

            var repositoryHits = await index.SearchAsync(
                identity,
                repositoryId,
                vector,
                new(),
                10
            );
            Assert.Equal(3, repositoryHits.Count);
            Assert.DoesNotContain(repositoryHits, hit => hit.MemoryId == "foreign-memory");
            Assert.Equal(
                2,
                (
                    await index.SearchAsync(
                        identity,
                        repositoryId,
                        vector,
                        new(Status: "validated"),
                        10
                    )
                ).Count
            );
            Assert.Equal(
                2,
                (
                    await index.SearchAsync(
                        identity,
                        repositoryId,
                        vector,
                        new(Category: "architecture"),
                        10
                    )
                ).Count
            );
            Assert.Equal(
                2,
                (
                    await index.SearchAsync(
                        identity,
                        repositoryId,
                        vector,
                        new(Tag: "caching"),
                        10
                    )
                ).Count
            );
            var combined = await index.SearchAsync(
                identity,
                repositoryId,
                vector,
                new(Status: "validated", Category: "architecture", Tag: "caching"),
                10
            );
            Assert.Equal("memory-one", Assert.Single(combined).MemoryId);
            Assert.Equal(
                "memory-one",
                Assert.Single(
                    await index.SearchAsync(
                        identity,
                        repositoryId,
                        vector,
                        new(DecisionArea: "storage"),
                        10
                    )
                ).MemoryId
            );

            var conflicting = Clone(architecture);
            conflicting.ContentHash = "different-hash";
            var conflict = await Assert.ThrowsAsync<ValidationException>(() =>
                index.UpsertAsync(identity, conflicting, vector)
            );
            Assert.Equal("revision_conflict", conflict.Code);

            var revisionTwo = Clone(architecture);
            revisionTwo.Revision = 2;
            revisionTwo.ContentHash = "hash-memory-one-revision-two";
            revisionTwo.UpdatedAtUtc = revisionTwo.UpdatedAtUtc.AddMinutes(1);
            await index.UpsertAsync(identity, revisionTwo, vector, expectedIndexedRevision: 1);

            var stale = Clone(revisionTwo);
            stale.Revision = 3;
            stale.ContentHash = "stale-write";
            var staleConflict = await Assert.ThrowsAsync<ValidationException>(() =>
                index.UpsertAsync(identity, stale, vector, expectedIndexedRevision: 1)
            );
            Assert.Equal("revision_conflict", staleConflict.Code);
            var final = await index.SearchAsync(
                identity,
                repositoryId,
                vector,
                new(Status: "validated", Category: "architecture", Tag: "caching"),
                10
            );
            Assert.Equal(2, Assert.Single(final).Revision);
        }
        finally
        {
            if (await client.CollectionExistsAsync(identity.PhysicalCollection))
                await client.DeleteCollectionAsync(identity.PhysicalCollection);
        }
    }

    [QdrantFact]
    [Trait("Category", "SemanticIntegration")]
    public async Task PendingWorkSurvivesOutageAndReconcilesMissingStaleAndOrphanPoints()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "vector-work-tests",
            Guid.NewGuid().ToString("N")
        );
        var prefix = "agent_memory_work_" + Guid.NewGuid().ToString("N");
        var clock = new TestClock(DateTimeOffset.UtcNow);
        VectorCollectionIdentity? identity = null;
        try
        {
            using (var probe = BuildProvider(root, prefix, clock, "http://localhost:11434"))
            {
                var ollama = probe.GetRequiredService<OllamaEmbeddingClient>();
                var model = await ollama.GetModelIdentityAsync();
                var vector = await ollama.EmbedAsync(["dimension probe"]);
                identity = EmbeddingProjection.Collection(
                    prefix,
                    "integration-repo",
                    EmbeddingProjection.Fingerprint(
                        "ollama",
                        model.Model,
                        model.Digest,
                        vector.Vectors.Single().Length
                    )
                );
            }

            string memoryId;
            using (var offline = BuildProvider(root, prefix, clock, "http://127.0.0.1:1"))
            {
                var saved = await offline
                    .GetRequiredService<CanonicalMemoryService>()
                    .RememberAsync(
                        new RememberMemoryRequest
                        {
                            OperationId = "offline-candidate",
                            Tier = RememberTier.LongCandidate,
                            Content = "Cache parsed templates by stable content hash.",
                            Category = "architecture",
                            Tags = ["caching"],
                            SessionId = "session-one",
                            AgentId = "agent-one",
                            TaskId = "task-one",
                        }
                    );
                memoryId = saved.MemoryId;
                var pending = Assert.Single(
                    Directory.GetFiles(root, memoryId + ".json", SearchOption.AllDirectories),
                    path => path.Contains("vector-pending", StringComparison.Ordinal)
                );
                var failed = await offline
                    .GetRequiredService<VectorIndexCoordinator>()
                    .ProcessPendingAsync(1);
                Assert.Equal(1, failed.Processed);
                Assert.Equal(0, failed.Indexed);
                Assert.Equal(1, failed.Deferred);
                Assert.Equal(1, failed.Errors);
                var deferred = System.Text.Json.JsonSerializer.Deserialize<VectorPendingWork>(
                    await File.ReadAllTextAsync(pending),
                    MemoryJson.Options
                )!;
                Assert.Equal(1, deferred.Attempts);
                Assert.Equal("dependency_unavailable", deferred.LastErrorCode);
            }

            clock.Advance(TimeSpan.FromSeconds(2));
            using (var live = BuildProvider(root, prefix, clock, "http://localhost:11434"))
            {
                var coordinator = live.GetRequiredService<VectorIndexCoordinator>();
                var recovered = await coordinator.ProcessPendingAsync(1);
                Assert.Equal(1, recovered.Indexed);
                var record = (
                    await live
                        .GetRequiredService<CanonicalMemoryService>()
                        .GetAsync(new(memoryId, IncludeHistory: true))
                ).Record!;
                Assert.Equal(EmbeddingState.Ready, record.EmbeddingState);
                Assert.Equal(VectorIndexState.Indexed, record.IndexState);
                Assert.NotNull(record.Embedding?.ModelDigest);
                Assert.DoesNotContain(
                    Directory.GetFiles(root, memoryId + ".json", SearchOption.AllDirectories),
                    path => path.Contains("vector-pending", StringComparison.Ordinal)
                );
                var point = await live
                    .GetRequiredService<QdrantVectorIndex>()
                    .GetPointStateAsync(identity!, "integration-repo", memoryId);
                Assert.Equal(record.Revision, point!.Revision);
                Assert.Equal(record.ContentHash, point.ContentHash);

                var outcome = await live
                    .GetRequiredService<CanonicalMemoryService>()
                    .RecordOutcomeAsync(
                        new(
                            memoryId,
                            "outcome-one",
                            record.Revision,
                            "session-one",
                            "task-one",
                            "agent-one",
                            OutcomeKind.NotApplicable,
                            ["integration:not-applicable"]
                        )
                    );
                Assert.Equal(2, outcome.Revision);
                var staleRepair = await coordinator.ProcessPendingAsync(1);
                Assert.Equal(1, staleRepair.Indexed);
                record = (
                    await live
                        .GetRequiredService<CanonicalMemoryService>()
                        .GetAsync(new(memoryId, IncludeHistory: true))
                ).Record!;
                point = await live
                    .GetRequiredService<QdrantVectorIndex>()
                    .GetPointStateAsync(identity!, "integration-repo", memoryId);
                Assert.Equal(record.Revision, point!.Revision);

                var qdrantClient = live.GetRequiredService<QdrantClient>();
                var orphanId = Guid.NewGuid();
                var query = (
                    await live
                        .GetRequiredService<OllamaEmbeddingClient>()
                        .EmbedAsync([EmbeddingProjection.Project(record)])
                ).Vectors.Single();
                await qdrantClient.UpsertAsync(
                    identity!.PhysicalCollection,
                    [
                        new PointStruct
                        {
                            Id = orphanId,
                            Vectors = query,
                            Payload =
                            {
                                ["memoryId"] = "orphan-memory",
                                ["repositoryId"] = "integration-repo",
                                ["contentHash"] = "orphan-hash",
                                ["embeddingFingerprint"] = identity.Fingerprint.Hash,
                                ["payloadHash"] = "orphan-payload",
                                ["revision"] = 1L,
                            },
                        },
                    ],
                    wait: true
                );
                var orphanRepair = await coordinator.ReconcileAsync(identity, 10);
                Assert.Equal(1, orphanRepair.OrphansDeleted);

                await qdrantClient.DeleteCollectionAsync(identity.PhysicalCollection);
                var missingRepair = await coordinator.ReconcileAsync(identity, 10);
                Assert.Equal(1, missingRepair.MissingOrStaleQueued);
                var rebuilt = await coordinator.ProcessPendingAsync(1);
                Assert.Equal(1, rebuilt.Indexed);
                var hits = await live
                    .GetRequiredService<QdrantVectorIndex>()
                    .SearchAsync(
                        identity,
                        "integration-repo",
                        query,
                        new(Status: "candidate", Category: "architecture", Tag: "caching"),
                        10
                    );
                Assert.Equal(memoryId, Assert.Single(hits).MemoryId);
            }
        }
        finally
        {
            if (identity is not null)
            {
                using var client = new QdrantClient("localhost", 6334);
                if (await client.CollectionExistsAsync(identity.PhysicalCollection))
                    await client.DeleteCollectionAsync(identity.PhysicalCollection);
            }
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [QdrantFact]
    [Trait("Category", "SemanticIntegration")]
    public async Task MigrationRetainsOldAliasOnFailureAndRecoversCrashAfterSwitch()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "vector-migration-tests",
            Guid.NewGuid().ToString("N")
        );
        var prefix = "agent_memory_migration_" + Guid.NewGuid().ToString("N");
        var clock = new TestClock(DateTimeOffset.UtcNow);
        VectorCollectionIdentity? oldIdentity = null;
        VectorCollectionIdentity? targetIdentity = null;
        try
        {
            string firstId;
            using (var seed = BuildProvider(root, prefix, clock, "http://localhost:11434"))
            {
                var ollama = seed.GetRequiredService<OllamaEmbeddingClient>();
                var model = await ollama.GetModelIdentityAsync();
                var dimensions = (
                    await ollama.EmbedAsync(["migration dimension probe"])
                ).Vectors.Single().Length;
                targetIdentity = EmbeddingProjection.Collection(
                    prefix,
                    "integration-repo",
                    EmbeddingProjection.Fingerprint(
                        "ollama",
                        model.Model,
                        model.Digest,
                        dimensions
                    )
                );
                oldIdentity = EmbeddingProjection.Collection(
                    prefix,
                    "integration-repo",
                    EmbeddingProjection.Fingerprint(
                        "ollama",
                        "previous-model",
                        "previous-digest",
                        dimensions
                    )
                );
                var vectorIndex = seed.GetRequiredService<QdrantVectorIndex>();
                await vectorIndex.EnsureCollectionAsync(oldIdentity);
                await vectorIndex.SwitchAliasAsync(
                    oldIdentity.Alias,
                    oldIdentity.PhysicalCollection
                );
                var memory = seed.GetRequiredService<CanonicalMemoryService>();
                firstId = (
                    await memory.RememberAsync(
                        new RememberMemoryRequest
                        {
                            OperationId = "migration-first",
                            Tier = RememberTier.LongCandidate,
                            Content = "Prefer bounded retries for local dependencies.",
                            Category = "operations",
                            Tags = ["reliability"],
                            SessionId = "session-one",
                            TaskId = "task-one",
                            AgentId = "agent-one",
                        }
                    )
                ).MemoryId;
                await memory.RememberAsync(
                    new RememberMemoryRequest
                    {
                        OperationId = "migration-second",
                        Tier = RememberTier.LongCandidate,
                        Content = "Keep canonical files sufficient for vector reconstruction.",
                        Category = "architecture",
                        Tags = ["rebuild"],
                        SessionId = "session-two",
                        TaskId = "task-two",
                        AgentId = "agent-two",
                    }
                );
            }

            using (var offline = BuildProvider(root, prefix, clock, "http://127.0.0.1:1"))
            {
                await Assert.ThrowsAnyAsync<Exception>(() =>
                    offline
                        .GetRequiredService<VectorMigrationService>()
                        .MigrateAsync("failed-migration")
                );
                Assert.Equal(
                    oldIdentity.PhysicalCollection,
                    await offline
                        .GetRequiredService<QdrantVectorIndex>()
                        .GetAliasTargetAsync(oldIdentity.Alias)
                );
            }

            using (var migrating = BuildProvider(root, prefix, clock, "http://localhost:11434"))
            {
                var memory = migrating.GetRequiredService<CanonicalMemoryService>();
                var migration = migrating.GetRequiredService<VectorMigrationService>();
                migration.BeforeAliasSwitch = async cancellationToken =>
                {
                    var current = (
                        await memory.GetAsync(new(firstId, IncludeHistory: true), cancellationToken)
                    ).Record!;
                    await memory.RecordOutcomeAsync(
                        new(
                            firstId,
                            "migration-concurrent-outcome",
                            current.Revision,
                            "session-three",
                            "task-three",
                            "agent-three",
                            OutcomeKind.NotApplicable,
                            ["integration:migration-concurrency"]
                        ),
                        cancellationToken
                    );
                };
                migration.AfterAliasSwitch = () => throw new IOException("simulated crash");
                await Assert.ThrowsAsync<IOException>(() =>
                    migration.MigrateAsync("recoverable-migration")
                );
                Assert.Equal(
                    targetIdentity.PhysicalCollection,
                    await migrating
                        .GetRequiredService<QdrantVectorIndex>()
                        .GetAliasTargetAsync(targetIdentity.Alias)
                );
            }

            using (var restarted = BuildProvider(root, prefix, clock, "http://localhost:11434"))
            {
                var recovered = await restarted
                    .GetRequiredService<VectorMigrationService>()
                    .RecoverAsync("recoverable-migration");
                Assert.NotNull(recovered);
                Assert.True(recovered.Recovered);
                Assert.Equal(2, recovered.IndexedPoints);
                Assert.Equal(targetIdentity.PhysicalCollection, recovered.ActiveCollection);
                Assert.True(
                    await restarted
                        .GetRequiredService<QdrantClient>()
                        .CollectionExistsAsync(oldIdentity.PhysicalCollection)
                );
                var current = (
                    await restarted
                        .GetRequiredService<CanonicalMemoryService>()
                        .GetAsync(new(firstId, IncludeHistory: true))
                ).Record!;
                var point = await restarted
                    .GetRequiredService<QdrantVectorIndex>()
                    .GetPointStateAsync(targetIdentity, "integration-repo", firstId);
                Assert.Equal(current.Revision, point!.Revision);
                var firstSuccess = await restarted
                    .GetRequiredService<CanonicalMemoryService>()
                    .RecordOutcomeAsync(
                        new(
                            firstId,
                            "validation-success-one",
                            current.Revision,
                            "validation-session-one",
                            "validation-task-one",
                            "validation-agent-one",
                            OutcomeKind.Success,
                            ["integration:validation-one"]
                        )
                    );
                var secondSuccess = await restarted
                    .GetRequiredService<CanonicalMemoryService>()
                    .RecordOutcomeAsync(
                        new(
                            firstId,
                            "validation-success-two",
                            firstSuccess.Revision,
                            "validation-session-two",
                            "validation-task-two",
                            "validation-agent-two",
                            OutcomeKind.Success,
                            ["integration:validation-two"]
                        )
                    );
                var validated = await restarted
                    .GetRequiredService<CanonicalMemoryService>()
                    .PromoteAsync(
                        new(
                            firstId,
                            "validate-live-candidate",
                            secondSuccess.Revision,
                            "validation-reviewer",
                            "Two independent integration successes"
                        )
                    );
                Assert.Equal(MemoryState.Validated, validated.Status);
                var validationIndex = await restarted
                    .GetRequiredService<VectorIndexCoordinator>()
                    .ProcessPendingAsync(10);
                Assert.True(validationIndex.Indexed >= 1);
                Assert.Contains(
                    Directory.GetFiles(root, "vector-active.json", SearchOption.AllDirectories),
                    path => File.Exists(path)
                );
                var memory = restarted.GetRequiredService<CanonicalMemoryService>();
                await memory.RememberAsync(
                    new RememberMemoryRequest
                    {
                        OperationId = "recall-temp",
                        Tier = RememberTier.Temp,
                        Content = "Bounded retries protect local services.",
                        Category = "operations",
                        SessionId = "session-recall",
                        AgentId = "agent-recall",
                    }
                );
                await memory.RememberAsync(
                    new RememberMemoryRequest
                    {
                        OperationId = "recall-short",
                        Tier = RememberTier.Short,
                        Content = "Bounded retries avoid runaway local service calls.",
                        Category = "operations",
                        SessionId = "session-recall",
                        AgentId = "agent-recall",
                    }
                );
                var recall = await memory.RecallAsync(
                    new()
                    {
                        Query = "How should retries for local services be controlled?",
                        MaxResults = 10,
                    }
                );
                Assert.Equal("semantic", recall.Mode);
                Assert.Empty(recall.HealthReasons);
                Assert.Contains(
                    recall.Memories,
                    hit => hit.Record.Id == firstId && hit.MatchType == "semantic"
                );
                Assert.Equal(
                    recall.Memories.Select(hit => hit.Record.Tier).Order().ToArray(),
                    recall.Memories.Select(hit => hit.Record.Tier).ToArray()
                );
                var strictSimilarity = await memory.RecallAsync(
                    new()
                    {
                        Query = "How should retries for local services be controlled?",
                        Tiers = [MemoryTier.Long],
                        IncludeCandidates = true,
                        MinimumSimilarity = 1,
                    }
                );
                Assert.Equal("semantic", strictSimilarity.Mode);
                Assert.DoesNotContain(
                    strictSimilarity.Memories,
                    hit => hit.Record.Id == firstId
                );

                var firstRecord = (
                    await memory.GetAsync(new(firstId, IncludeHistory: true))
                ).Record!;
                var queryVector = (
                    await restarted
                        .GetRequiredService<OllamaEmbeddingClient>()
                        .EmbedAsync(
                            ["How should retries for local services be controlled?"],
                            targetIdentity.Fingerprint.Dimension
                        )
                ).Vectors.Single();
                await restarted.GetRequiredService<QdrantClient>().UpsertAsync(
                    targetIdentity.PhysicalCollection,
                    [
                        new PointStruct
                        {
                            Id = QdrantVectorIndex.PointId("integration-repo", firstId),
                            Vectors = queryVector,
                            Payload =
                            {
                                ["memoryId"] = firstId,
                                ["repositoryId"] = "integration-repo",
                                ["status"] = "validated",
                                ["category"] = firstRecord.Category,
                                ["decisionArea"] = firstRecord.DecisionArea ?? string.Empty,
                                ["tags"] = firstRecord.Tags.ToArray(),
                                ["contentHash"] = firstRecord.ContentHash,
                                ["embeddingFingerprint"] = targetIdentity.Fingerprint.Hash,
                                ["payloadHash"] = "deliberately-stale-payload",
                                ["revision"] = firstRecord.Revision + 100,
                            },
                        },
                    ],
                    wait: true
                );
                var canonicalCheck = await memory.RecallAsync(
                    new()
                    {
                        Query = "How should retries for local services be controlled?",
                        Tiers = [MemoryTier.Long],
                        MemoryIds = [firstId],
                        IncludeCandidates = true,
                    }
                );
                Assert.DoesNotContain(
                    canonicalCheck.Memories,
                    hit => hit.Record.Id == firstId
                );
            }

            using (var noOllama = BuildProvider(root, prefix, clock, "http://127.0.0.1:1"))
            {
                var degraded = await noOllama
                    .GetRequiredService<CanonicalMemoryService>()
                    .RecallAsync(
                        new()
                        {
                            Query = "bounded retries",
                            IncludeCandidates = true,
                        }
                    );
                Assert.Equal("degraded_lexical", degraded.Mode);
                Assert.Contains("embedding_unavailable", degraded.HealthReasons);
                Assert.Contains(
                    degraded.Memories,
                    hit => hit.Record.Id == firstId && hit.MatchType == "lexical"
                );
            }

            using (var noQdrant = BuildProvider(
                root,
                prefix,
                clock,
                "http://localhost:11434",
                qdrantPort: 1
            ))
            {
                var degraded = await noQdrant
                    .GetRequiredService<CanonicalMemoryService>()
                    .RecallAsync(
                        new()
                        {
                            Query = "bounded retries",
                            IncludeCandidates = true,
                        }
                    );
                Assert.Equal("degraded_lexical", degraded.Mode);
                Assert.Contains("vector_index_unavailable", degraded.HealthReasons);
            }
        }
        finally
        {
            using var client = new QdrantClient("localhost", 6334);
            foreach (
                var collection in new[]
                {
                    oldIdentity?.PhysicalCollection,
                    targetIdentity?.PhysicalCollection,
                }.Where(name => name is not null).Distinct(StringComparer.Ordinal)
            )
                if (await client.CollectionExistsAsync(collection!))
                    await client.DeleteCollectionAsync(collection!);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static MemoryRecord Record(
        string id,
        string repositoryId,
        EmbeddingFingerprint fingerprint,
        MemoryState status,
        string category,
        List<string> tags
    ) =>
        new()
        {
            Id = id,
            RepositoryId = repositoryId,
            Tier = MemoryTier.Long,
            Content = "integration claim",
            Category = category,
            Tags = tags,
            ContentHash = "hash-" + id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            OperationId = "operation-" + id,
            Status = status,
            EmbeddingState = EmbeddingState.Ready,
            IndexState = VectorIndexState.Pending,
            Embedding = new(
                fingerprint.Provider,
                fingerprint.Model,
                fingerprint.Dimension,
                fingerprint.ProjectionVersion,
                null,
                fingerprint.ModelDigest
            ),
        };

    private static MemoryRecord Clone(MemoryRecord record) =>
        System.Text.Json.JsonSerializer.Deserialize<MemoryRecord>(
            System.Text.Json.JsonSerializer.Serialize(record, MemoryJson.Options),
            MemoryJson.Options
        )!;

    private static ServiceProvider BuildProvider(
        string root,
        string collection,
        TimeProvider time,
        string ollamaBaseUrl,
        int qdrantPort = 6334
    ) =>
        new ServiceCollection()
            .AddSingleton(time)
            .AddMemoryStorageConfiguration(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["SystemStorage:Root"] = root,
                            ["Repository:Id"] = "integration-repo",
                            ["Embedding:Ollama:BaseUrl"] = ollamaBaseUrl,
                            ["Qdrant:Collection"] = collection,
                            ["Qdrant:GrpcPort"] = qdrantPort.ToString(
                                System.Globalization.CultureInfo.InvariantCulture
                            ),
                        }
                    )
                    .Build()
            )
            .BuildServiceProvider();

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan value) => now += value;
    }
}

public sealed class QdrantFactAttribute : FactAttribute
{
    public QdrantFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("AGENT_MEMORY_RUN_QDRANT_TESTS") != "1")
            Skip = "Set AGENT_MEMORY_RUN_QDRANT_TESTS=1 to run against isolated localhost Qdrant.";
    }
}
