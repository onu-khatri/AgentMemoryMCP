using System.Text.Json;

namespace AgentSession.MCP.Tests;

public sealed class McpSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "mcp-session-tests",
        Guid.NewGuid().ToString("N")
    );

    [Theory]
    [InlineData("2025-06-18")]
    [InlineData("2025-11-25")]
    [InlineData("2026-07-28")]
    public async Task ProtocolDiscoveryHasOnlyUniqueSupportedTools(string version)
    {
        using var server = new McpProcess(_root, version: version);
        await server.InitializeAsync();
        var result = await server.RequestAsync("tools/list", new { });
        var tools = result.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
        var names = tools.Select(x => x.GetProperty("name").GetString()).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
        Assert.DoesNotContain("create_agent_artifact", names);
        Assert.DoesNotContain("save_final_plan", names);
        string[] requiredSessionTools =
        [
            "create_or_activate_session",
            "list_agent_sessions",
            "append_agent_memory",
            "coordinate_agent_task",
            "resume_agent_session",
            "checkpoint_agent_session",
        ];
        string[] requiredMemoryTools =
        [
            "memory_status",
            "memory_remember",
            "memory_recall",
            "memory_get",
            "memory_list_review",
            "memory_mark_reviewed",
            "memory_compact",
            "memory_delete",
            "memory_archive",
            "memory_record_event",
            "memory_record_outcome",
            "memory_promote",
            "memory_supersede",
            "memory_retire",
            "memory_reindex",
            "memory_cleanup",
            "memory_rebuild_indexes",
            "memory_prepare_compaction",
            "memory_commit_compaction",
            "memory_update",
        ];
        var requiredTools = requiredSessionTools.Concat(requiredMemoryTools).ToArray();
        Assert.Equal(26, requiredTools.Length);
        Assert.Equal(requiredTools.Length, names.Length);
        Assert.All(requiredTools, name => Assert.Single(names, candidate => candidate == name));
        foreach (var name in requiredTools)
        {
            var tool = Assert.Single(tools, candidate => candidate.GetProperty("name").GetString() == name);
            Assert.Equal(JsonValueKind.Object, tool.GetProperty("inputSchema").ValueKind);
            Assert.Equal(JsonValueKind.Object, tool.GetProperty("outputSchema").ValueKind);
            Assert.Equal(JsonValueKind.Object, tool.GetProperty("annotations").ValueKind);
            var description = tool.GetProperty("description").GetString();
            Assert.False(string.IsNullOrWhiteSpace(description));
            Assert.Contains("Use ", description, StringComparison.Ordinal);
            Assert.Contains("Do not ", description, StringComparison.Ordinal);
        }
        string Description(string name) =>
            Assert.Single(tools, candidate => candidate.GetProperty("name").GetString() == name)
                .GetProperty("description")
                .GetString()!;
        Assert.Contains("primary persisted session-memory", Description("resume_agent_session"));
        Assert.Contains("does not save context", Description("checkpoint_agent_session"));
        Assert.Contains("memory_remember", Description("append_agent_memory"));
        Assert.Contains("authoritative session task-state", Description("coordinate_agent_task"));
        Assert.Contains("primary persisted reusable-memory", Description("memory_recall"));
        Assert.Contains("append_agent_memory", Description("memory_remember"));
        Assert.Contains("one exact canonical", Description("memory_get"));
        Assert.Contains("review state only", Description("memory_mark_reviewed"));
        Assert.Contains("rebuild vectors or migrate embeddings", Description("memory_rebuild_indexes"));
        var reindexTool = Assert.Single(
            tools,
            candidate => candidate.GetProperty("name").GetString() == "memory_reindex"
        );
        Assert.Contains("\"maximum\":100", reindexTool.GetProperty("inputSchema").GetRawText());
        Assert.Contains("\"maxItems\":100", reindexTool.GetProperty("inputSchema").GetRawText());
        var append = Assert.Single(
            tools,
            x => x.GetProperty("name").GetString() == "append_agent_memory"
        );
        Assert.Contains("updates", append.GetProperty("inputSchema").ToString());
        Assert.DoesNotContain("cancellationToken", append.GetProperty("inputSchema").ToString());
        var active = await server.CallAsync(
            "create_or_activate_session",
            new
            {
                sessionId = "session",
                actorId = "agent-a",
                operationId = "activate",
            }
        );
        Assert.Equal("session", active.GetProperty("sessionId").GetString());
    }

    [Fact]
    public async Task ObservationEventToolAcceptsTypedInputAndRejectsReservedLifecycleType()
    {
        using var server = new McpProcess(_root);
        await server.InitializeAsync();
        var recorded = await server.CallAsync(
            "memory_record_event",
            new
            {
                eventId = "observation-mcp",
                eventType = "observation-test-result",
                agentId = "agent-a",
                sessionId = "session",
                metadata = new Dictionary<string, string> { ["status"] = "passed" },
            }
        );
        Assert.Equal("observation-mcp", recorded.GetProperty("eventId").GetString());
        var malformed = await server.RequestAsync(
            "tools/call",
            new
            {
                name = "memory_record_event",
                arguments = new
                {
                    request = new
                    {
                        eventId = "spoofed-mcp",
                        eventType = "memory-validated",
                        agentId = "agent-a",
                    },
                },
            }
        );
        Assert.True(malformed.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Contains("event_type_reserved", malformed.ToString());
    }

    [Fact]
    public async Task CompactionToolsRoundTripReviewedSourcesAndRejectIncompleteCoverage()
    {
        using var server = new McpProcess(_root);
        await server.InitializeAsync();
        async Task<(string Id, long Revision)> CreateAndReview(string suffix, string content, string evidence)
        {
            var created = await server.CallAsync(
                "memory_remember",
                new
                {
                    operationId = "create-" + suffix,
                    tier = "short",
                    content,
                    sessionId = "session",
                    taskId = "task",
                    agentId = "agent-a",
                }
            );
            var id = created.GetProperty("memoryId").GetString()!;
            var reviewed = await server.CallAsync(
                "memory_mark_reviewed",
                new
                {
                    memoryId = id,
                    operationId = "review-" + suffix,
                    expectedRevision = created.GetProperty("revision").GetInt64(),
                    agentId = "agent-a",
                    action = "compact_candidate",
                    reason = "Reviewed for compaction",
                    evidence = new[] { evidence },
                }
            );
            return (id, reviewed.GetProperty("revision").GetInt64());
        }

        var first = await CreateAndReview("first", "First reviewed source.", "test:first");
        var second = await CreateAndReview("second", "Second reviewed source.", "test:second");
        var sources = new[]
        {
            new { memoryId = first.Id, expectedRevision = first.Revision },
            new { memoryId = second.Id, expectedRevision = second.Revision },
        };
        var prepared = await server.CallAsync(
            "memory_prepare_compaction",
            new { operationId = "prepare", agentId = "agent-a", sources }
        );
        var token = prepared.GetProperty("preparationToken").GetString();
        var compacted = await server.CallAsync(
            "memory_commit_compaction",
            new
            {
                operationId = "commit",
                preparationToken = token,
                agentId = "agent-a",
                reason = "Reviewed summary",
                title = "Combined finding",
                summary = "The two reviewed sources form one combined finding.",
                facts = new[] { "Both sources were reviewed." },
                openQuestions = Array.Empty<string>(),
                contradictions = Array.Empty<string>(),
                successfulPatterns = Array.Empty<string>(),
                failedPatterns = Array.Empty<string>(),
                evidence = new[] { "test:first", "test:second" },
                sources,
                sourceCoverage = new object[]
                {
                    new { memoryId = first.Id, evidence = new[] { "test:first" }, contradictions = Array.Empty<string>() },
                    new { memoryId = second.Id, evidence = new[] { "test:second" }, contradictions = Array.Empty<string>() },
                },
                tags = new[] { "mcp" },
                sessionId = "session",
                taskId = "task",
            }
        );
        Assert.Equal(2, compacted.GetProperty("archivedMemoryIds").GetArrayLength());

        var malformed = await server.RequestAsync(
            "tools/call",
            new
            {
                name = "memory_commit_compaction",
                arguments = new
                {
                    request = new
                    {
                        operationId = "commit-incomplete",
                        preparationToken = token,
                        agentId = "agent-a",
                        reason = "Missing coverage",
                        title = "Invalid",
                        summary = "Invalid request",
                        facts = Array.Empty<string>(),
                        openQuestions = Array.Empty<string>(),
                        contradictions = Array.Empty<string>(),
                        successfulPatterns = Array.Empty<string>(),
                        failedPatterns = Array.Empty<string>(),
                        evidence = Array.Empty<string>(),
                        sources,
                        sourceCoverage = Array.Empty<object>(),
                        tags = Array.Empty<string>(),
                    },
                },
            }
        );
        Assert.True(malformed.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task HostedMaintenancePublishesBoundedRunFieldsWithoutSemanticDecisions()
    {
        using var server = new McpProcess(_root);
        await server.InitializeAsync();
        var created = await server.CallAsync(
            "memory_remember",
            new
            {
                operationId = "maintenance-short",
                tier = "short",
                content = "Unreviewed short knowledge must remain active.",
                agentId = "agent-a",
            }
        );
        JsonElement status = default;
        var deadline = DateTime.UtcNow.AddSeconds(12);
        do
        {
            status = await server.CallWithoutRequestAsync("memory_status");
            if (status.GetProperty("lastCleanupAtUtc").ValueKind != JsonValueKind.Null)
                break;
            await Task.Delay(50);
        } while (DateTime.UtcNow < deadline);

        Assert.NotEqual(JsonValueKind.Null, status.GetProperty("lastCleanupAtUtc").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, status.GetProperty("lastIndexRepairAtUtc").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, status.GetProperty("lastEmbeddingRunAtUtc").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, status.GetProperty("lastReconciliationAtUtc").ValueKind);
        Assert.Equal("degraded", status.GetProperty("qdrant").GetProperty("state").GetString());
        Assert.Equal(
            "semantic_active_collection_missing",
            status.GetProperty("qdrant").GetProperty("errorCode").GetString()
        );
        var record = await server.CallAsync(
            "memory_get",
            new { memoryId = created.GetProperty("memoryId").GetString() }
        );
        Assert.Equal("unreviewed", record.GetProperty("record").GetProperty("reviewState").GetString());
        Assert.Equal("active", record.GetProperty("record").GetProperty("status").GetString());
    }

    [Fact]
    public async Task StructuredContextSurvivesRestartAndLegacyPayloadDoesNotWrite()
    {
        using (var server = new McpProcess(_root))
        {
            await server.InitializeAsync();
            await server.CallAsync(
                "create_or_activate_session",
                new
                {
                    sessionId = "session",
                    actorId = "agent-a",
                    operationId = "activate",
                }
            );
            var request = new
            {
                sessionId = "session",
                actorId = "agent-a",
                operationId = "context",
                updates = new object[]
                {
                    new
                    {
                        expectedRevision = 0,
                        artifact = new
                        {
                            id = "context",
                            kind = "context",
                            actorId = "agent-a",
                            title = "Shared context",
                            context = new
                            {
                                objective = "Finish parser",
                                planReferences = new[] { "plan-1" },
                                constraints = Array.Empty<string>(),
                                nextActions = new[] { "Verify parser" },
                            },
                        },
                    },
                    new
                    {
                        expectedRevision = 0,
                        artifact = new
                        {
                            id = "plan-1",
                            kind = "plan",
                            actorId = "agent-a",
                            title = "Parser plan",
                            data = new
                            {
                                acceptanceCriteria = new[] { "Reject malformed tokens" },
                                steps = new[] { "Build parser", "Test errors" },
                            },
                        },
                    },
                },
            };
            await server.CallAsync("append_agent_memory", request);
            var retry = await server.CallAsync("append_agent_memory", request);
            Assert.Equal(
                1,
                retry.GetProperty("artifactRevisions").GetProperty("context").GetInt64()
            );
            var obsolete = await server.RequestAsync(
                "tools/call",
                new
                {
                    name = "append_agent_memory",
                    arguments = new
                    {
                        request = new
                        {
                            sessionId = "session",
                            content = "obsolete-freeform",
                            agentName = "agent-a",
                        },
                    },
                }
            );
            Assert.True(
                obsolete.TryGetProperty("error", out _)
                    || obsolete.GetProperty("result").GetProperty("isError").GetBoolean()
            );
        }
        using var restarted = new McpProcess(_root);
        await restarted.InitializeAsync();
        var page = await restarted.CallAsync(
            "resume_agent_session",
            new { sessionId = "session", actorId = "agent-b" }
        );
        Assert.Contains("Finish parser", page.ToString());
        Assert.Contains("Verify parser", page.ToString());
        Assert.Contains("Reject malformed tokens", page.ToString());
        Assert.DoesNotContain("obsolete-freeform", page.ToString());
        Assert.Equal(0, page.GetProperty("acknowledgedSequence").GetInt64());
        await restarted.CallAsync(
            "checkpoint_agent_session",
            new
            {
                sessionId = "session",
                actorId = "agent-b",
                snapshotId = page.GetProperty("snapshotId").GetString(),
                sequence = page.GetProperty("sequence").GetInt64(),
            }
        );
    }

    [Fact]
    public async Task TwoAgentsShareCompletedOutputsAndRepositoriesStayIsolated()
    {
        using var first = new McpProcess(_root);
        using var second = new McpProcess(_root);
        await first.InitializeAsync();
        await second.InitializeAsync();
        await first.CallAsync(
            "coordinate_agent_task",
            new
            {
                sessionId = "shared",
                actorId = "agent-a",
                operationId = "create",
                taskId = "parser",
                action = "create",
                workKey = "parser-work",
                title = "Implement parser",
            }
        );
        var claimed = await first.CallAsync(
            "coordinate_agent_task",
            new
            {
                sessionId = "shared",
                actorId = "agent-a",
                operationId = "claim",
                taskId = "parser",
                action = "claim",
                expectedRevision = 1,
            }
        );
        var token = claimed.GetProperty("task").GetProperty("claimToken").GetString();
        var denied = await second.RequestAsync(
            "tools/call",
            new
            {
                name = "coordinate_agent_task",
                arguments = new
                {
                    request = new
                    {
                        sessionId = "shared",
                        actorId = "agent-b",
                        operationId = "compete",
                        taskId = "parser",
                        action = "claim",
                        expectedRevision = 2,
                    },
                },
            }
        );
        Assert.True(denied.GetProperty("result").GetProperty("isError").GetBoolean());
        await first.CallAsync(
            "coordinate_agent_task",
            new
            {
                sessionId = "shared",
                actorId = "agent-a",
                operationId = "complete",
                taskId = "parser",
                action = "complete",
                expectedRevision = 2,
                claimToken = token,
                outputs = new[] { "src/parser.cs" },
                evidence = new[] { "test-report-1" },
                verificationStatus = "passed",
                handoff = "Reuse the parser",
            }
        );
        var resumed = await second.CallAsync(
            "resume_agent_session",
            new { sessionId = "shared", actorId = "agent-b" }
        );
        Assert.Contains("src/parser.cs", resumed.ToString());
        Assert.Contains("test-report-1", resumed.ToString());
        Assert.Contains("completed", resumed.ToString());
        using var otherRepo = new McpProcess(_root, "other-repo");
        await otherRepo.InitializeAsync();
        var listing = await otherRepo.CallAsync("list_agent_sessions", new { });
        Assert.Empty(listing.GetProperty("sessionIds").EnumerateArray());
        var absent = await otherRepo.RequestAsync(
            "tools/call",
            new
            {
                name = "resume_agent_session",
                arguments = new { request = new { sessionId = "shared", actorId = "agent-b" } },
            }
        );
        Assert.True(absent.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task CancellationWhileWaitingForRepositoryLockCreatesNoSession()
    {
        using var server = new McpProcess(_root);
        await server.InitializeAsync();
        using (var locker = new WorkerProcess("lock", _root))
        {
            Assert.Equal("locked", await locker.ReadLineAsync());
            var requestId = await server.SendAsync(
                "tools/call",
                new
                {
                    name = "create_or_activate_session",
                    arguments = new
                    {
                        request = new
                        {
                            sessionId = "canceled",
                            actorId = "agent-a",
                            operationId = "cancel-create",
                        },
                    },
                }
            );
            await server.NotifyAsync(
                "notifications/cancelled",
                new { requestId, reason = "test cancellation" }
            );
            // A cancelled request need not produce a response. A later ping confirms that the
            // connection is responsive while the repository is still locked by another process.
            var response = await server.RequestAsync("ping", new { });
            Assert.True(response.TryGetProperty("result", out _));
        }
        var listing = await server.CallAsync("list_agent_sessions", new { });
        Assert.Empty(listing.GetProperty("sessionIds").EnumerateArray());
    }

    [Fact]
    public async Task SharedContextRejectsAlternativeIdsAndStaleRevisions()
    {
        using var server = new McpProcess(_root);
        await server.InitializeAsync();
        object Contribution(string id, string operation, long revision) =>
            new
            {
                sessionId = "shared",
                actorId = "agent-a",
                operationId = operation,
                updates = new[]
                {
                    new
                    {
                        expectedRevision = revision,
                        artifact = new
                        {
                            id,
                            kind = "context",
                            actorId = "agent-a",
                            title = "Context",
                            context = new { objective = "Shared objective" },
                        },
                    },
                },
            };
        var invalid = await server.RequestAsync(
            "tools/call",
            new
            {
                name = "append_agent_memory",
                arguments = new { request = Contribution("agent-context", "invalid", 0) },
            }
        );
        Assert.True(invalid.GetProperty("result").GetProperty("isError").GetBoolean());
        await server.CallAsync("append_agent_memory", Contribution("context", "create", 0));
        var stale = await server.RequestAsync(
            "tools/call",
            new
            {
                name = "append_agent_memory",
                arguments = new { request = Contribution("context", "stale", 0) },
            }
        );
        Assert.True(stale.GetProperty("result").GetProperty("isError").GetBoolean());
        var page = await server.CallAsync(
            "resume_agent_session",
            new { sessionId = "shared", actorId = "reader" }
        );
        var unknown = page.GetProperty("unknown")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToArray();
        Assert.DoesNotContain("objective", unknown);
        Assert.Contains("constraints", unknown);
    }

    [Fact]
    public async Task ExpiredClaimCanBeTakenOverAfterServerRestart()
    {
        string? staleToken;
        DateTimeOffset expiry;
        using (var first = new McpProcess(_root, leaseMinutes: 0.02))
        {
            await first.InitializeAsync();
            await first.CallAsync(
                "coordinate_agent_task",
                new
                {
                    sessionId = "shared",
                    actorId = "agent-a",
                    operationId = "create",
                    taskId = "work",
                    action = "create",
                    workKey = "work",
                    title = "Recoverable work",
                }
            );
            var claimed = await first.CallAsync(
                "coordinate_agent_task",
                new
                {
                    sessionId = "shared",
                    actorId = "agent-a",
                    operationId = "claim",
                    taskId = "work",
                    action = "claim",
                    expectedRevision = 1,
                }
            );
            staleToken = claimed.GetProperty("task").GetProperty("claimToken").GetString();
            expiry = claimed
                .GetProperty("task")
                .GetProperty("claimExpiresAtUtc")
                .GetDateTimeOffset();
        }
        var remaining = expiry - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining + TimeSpan.FromMilliseconds(25));
        using var next = new McpProcess(_root);
        await next.InitializeAsync();
        var takeover = await next.CallAsync(
            "coordinate_agent_task",
            new
            {
                sessionId = "shared",
                actorId = "agent-b",
                operationId = "takeover",
                taskId = "work",
                action = "claim",
                expectedRevision = 2,
            }
        );
        Assert.NotEqual(
            staleToken,
            takeover.GetProperty("task").GetProperty("claimToken").GetString()
        );
        var stale = await next.RequestAsync(
            "tools/call",
            new
            {
                name = "coordinate_agent_task",
                arguments = new
                {
                    request = new
                    {
                        sessionId = "shared",
                        actorId = "agent-a",
                        operationId = "late-completion",
                        taskId = "work",
                        action = "complete",
                        expectedRevision = 3,
                        claimToken = staleToken,
                        handoff = "Obsolete owner's report",
                    },
                },
            }
        );
        Assert.True(stale.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task CursorAndCheckpointCatchUpSurviveRestartWithConcurrentContribution()
    {
        string? cursor;
        long snapshotSequence;
        using (var first = new McpProcess(_root))
        {
            await first.InitializeAsync();
            await first.CallAsync(
                "coordinate_agent_task",
                new
                {
                    sessionId = "shared",
                    actorId = "agent-a",
                    operationId = "create",
                    taskId = "work",
                    action = "create",
                    workKey = "work",
                    title = "Initial work",
                }
            );
            var page = await first.CallAsync(
                "resume_agent_session",
                new
                {
                    sessionId = "shared",
                    actorId = "reader",
                    pageSize = 1,
                }
            );
            cursor = page.GetProperty("cursor").GetString();
            snapshotSequence = page.GetProperty("sequence").GetInt64();
            using var contributor = new McpProcess(_root);
            await contributor.InitializeAsync();
            await contributor.CallAsync(
                "append_agent_memory",
                new
                {
                    sessionId = "shared",
                    actorId = "agent-b",
                    operationId = "finding",
                    updates = new[]
                    {
                        new
                        {
                            expectedRevision = 0,
                            artifact = new
                            {
                                id = "finding",
                                kind = "finding",
                                actorId = "agent-b",
                                title = "Later contribution",
                                data = new { conclusion = "New evidence" },
                            },
                        },
                    },
                }
            );
        }
        using (var restarted = new McpProcess(_root))
        {
            await restarted.InitializeAsync();
            var oldPage = await restarted.CallAsync(
                "resume_agent_session",
                new
                {
                    sessionId = "shared",
                    actorId = "reader",
                    pageSize = 1,
                    cursor,
                }
            );
            Assert.False(oldPage.GetProperty("hasMore").GetBoolean());
            Assert.Equal(snapshotSequence, oldPage.GetProperty("sequence").GetInt64());
            Assert.DoesNotContain("Later contribution", oldPage.ToString());
            await restarted.CallAsync(
                "checkpoint_agent_session",
                new
                {
                    sessionId = "shared",
                    actorId = "reader",
                    snapshotId = oldPage.GetProperty("snapshotId").GetString(),
                    sequence = snapshotSequence,
                }
            );
        }
        using var final = new McpProcess(_root);
        await final.InitializeAsync();
        var catchUp = await final.CallAsync(
            "resume_agent_session",
            new { sessionId = "shared", actorId = "reader" }
        );
        Assert.Equal(snapshotSequence, catchUp.GetProperty("acknowledgedSequence").GetInt64());
        Assert.Contains("Later contribution", catchUp.ToString());
        Assert.Single(
            catchUp.GetProperty("entries").EnumerateArray(),
            entry => entry.GetProperty("kind").GetString() == "change"
        );
    }

    [Fact]
    public async Task StartupAndActivationLeaveHistoricalFilesUntouched()
    {
        var historical = Path.Combine(_root, "historical-sessions", "old-session");
        Directory.CreateDirectory(historical);
        var file = Path.Combine(historical, "memory.md");
        var bytes = System.Text.Encoding.UTF8.GetBytes("Historical session content\r\n");
        await File.WriteAllBytesAsync(file, bytes);
        using var server = new McpProcess(Path.Combine(_root, "central"));
        await server.InitializeAsync();
        await server.CallAsync(
            "create_or_activate_session",
            new
            {
                sessionId = "new-session",
                actorId = "agent-a",
                operationId = "activate",
            }
        );
        Assert.Equal(bytes, await File.ReadAllBytesAsync(file));
        Assert.Single(Directory.EnumerateFiles(historical));
        Assert.False(
            Directory.Exists(Path.Combine(_root, "central", "sessions", "repo", "old-session"))
        );
    }

    [Fact]
    public async Task ConcurrentEquivalentClaimsCreateOneCanonicalRecord()
    {
        using var first = new McpProcess(_root);
        using var second = new McpProcess(_root);
        await first.InitializeAsync();
        await second.InitializeAsync();

        object Request(string operationId, string content) =>
            new
            {
                operationId,
                tier = "short",
                content,
                sessionId = "session",
                taskId = "task",
                agentId = "agent-a",
                category = "implementation",
                decisionArea = "parser",
            };

        var writes = await Task.WhenAll(
            first.CallAsync(
                "memory_remember",
                Request("concurrent-first", "Parser handles caf\u00e9\r\ninput")
            ),
            second.CallAsync(
                "memory_remember",
                Request("concurrent-second", "Parser handles cafe\u0301\ninput")
            )
        );

        Assert.Equal(
            writes[0].GetProperty("memoryId").GetString(),
            writes[1].GetProperty("memoryId").GetString()
        );
        Assert.Single(writes, write => write.GetProperty("duplicate").GetBoolean());
        Assert.Single(
            Directory.GetFiles(
                Path.Combine(_root, "repositories", "repo", "AiLearning", "short-term"),
                "*.json",
                SearchOption.AllDirectories
            )
        );

        var conflicting = await first.CallAsync(
            "memory_remember",
            Request("distinct-claim", "Parser does not handle caf\u00e9 input")
        );
        Assert.NotEqual(
            writes[0].GetProperty("memoryId").GetString(),
            conflicting.GetProperty("memoryId").GetString()
        );
    }

    [Fact]
    public async Task MemoryToolsPersistRecallPromoteAndReviewAcrossRestart()
    {
        string? shortId;
        using (var first = new McpProcess(_root))
        {
            await first.InitializeAsync();
            var created = await first.CallAsync(
                "memory_remember",
                new
                {
                    operationId = "remember",
                    tier = "temp",
                    content = "Parser handles Unicode",
                    sessionId = "session",
                    agentId = "agent-a",
                    tags = new[] { "parser" },
                }
            );
            var id = created.GetProperty("memoryId").GetString();
            var recalled = await first.CallAsync(
                "memory_recall",
                new { query = "Unicode", tags = new[] { "parser" } }
            );
            Assert.Contains("Parser handles Unicode", recalled.ToString());
            var updated = await first.CallAsync(
                "memory_update",
                new
                {
                    memoryId = id,
                    operationId = "update",
                    expectedRevision = 1,
                    agentId = "agent-a",
                    content = "Parser handles Unicode and reports errors",
                }
            );
            Assert.Equal(2, updated.GetProperty("revision").GetInt64());
            var promoted = await first.CallAsync(
                "memory_promote",
                new
                {
                    memoryId = id,
                    operationId = "promote",
                    expectedRevision = 2,
                    agentId = "agent-a",
                    reason = "Retain useful parser behavior",
                }
            );
            shortId = promoted.GetProperty("memoryId").GetString();
        }
        using var restarted = new McpProcess(_root);
        await restarted.InitializeAsync();
        var record = await restarted.CallAsync("memory_get", new { memoryId = shortId });
        Assert.Equal("short", record.GetProperty("record").GetProperty("tier").GetString());
        var currentRevision = record.GetProperty("record").GetProperty("revision").GetInt64();
        var reviewed = false;
        for (var attempt = 1; attempt <= 3 && !reviewed; attempt++)
        {
            var response = await restarted.RequestAsync(
                "tools/call",
                new
                {
                    name = "memory_mark_reviewed",
                    arguments = new
                    {
                        request = new
                        {
                            memoryId = shortId,
                            operationId = "review-" + attempt,
                            expectedRevision = currentRevision,
                            agentId = "reviewer",
                            action = "delete_candidate",
                            reason = "Evaluate obsolescence",
                            evidence = new[] { "review-note" },
                        },
                    },
                }
            );
            var result = response.GetProperty("result");
            if (!result.TryGetProperty("isError", out var isError) || !isError.GetBoolean())
            {
                reviewed = true;
                break;
            }
            Assert.Contains("revision_conflict", response.ToString());
            var reread = await restarted.CallAsync("memory_get", new { memoryId = shortId });
            var nextRevision = reread.GetProperty("record").GetProperty("revision").GetInt64();
            // Startup index repair can change the catalog generation without changing the
            // canonical record. Both that race and a record update require a bounded reread.
            Assert.True(nextRevision >= currentRevision);
            currentRevision = nextRevision;
        }
        Assert.True(reviewed, "Review should succeed after bounded revision-conflict reconciliation.");
        Assert.Equal(
            "available",
            (await restarted.CallAsync("memory_get", new { memoryId = shortId }))
                .GetProperty("availability")
                .GetString()
        );
    }

    [Fact]
    public async Task LearningCleanupDoesNotRemoveDurableSessionProgress()
    {
        using var server = new McpProcess(_root);
        await server.InitializeAsync();
        await server.CallAsync(
            "create_or_activate_session",
            new
            {
                sessionId = "durable-session",
                actorId = "agent-a",
                operationId = "activate-durable-session",
            }
        );
        await server.CallAsync(
            "append_agent_memory",
            new
            {
                sessionId = "durable-session",
                actorId = "agent-a",
                operationId = "save-durable-context",
                updates = new[]
                {
                    new
                    {
                        expectedRevision = 0,
                        artifact = new
                        {
                            id = "context",
                            kind = "context",
                            actorId = "agent-a",
                            title = "Durable context",
                            context = new
                            {
                                objective = "Preserve session progress during learning cleanup",
                                nextActions = new[] { "Resume this work" },
                            },
                        },
                    },
                },
            }
        );
        var temp = await server.CallAsync(
            "memory_remember",
            new
            {
                operationId = "expiring-learning",
                tier = "temp",
                content = "Disposable temporary learning",
                sessionId = "durable-session",
                agentId = "agent-a",
                expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(2),
            }
        );
        await Task.Delay(TimeSpan.FromMilliseconds(2200));
        var cleanup = await server.CallWithoutRequestAsync("memory_cleanup");
        Assert.True(cleanup.GetProperty("deleted").GetInt32() >= 1);
        var expired = await server.CallAsync(
            "memory_get",
            new { memoryId = temp.GetProperty("memoryId").GetString() }
        );
        Assert.NotEqual("available", expired.GetProperty("availability").GetString());
        var resumed = await server.CallAsync(
            "resume_agent_session",
            new { sessionId = "durable-session", actorId = "agent-b" }
        );
        Assert.Contains("Preserve session progress during learning cleanup", resumed.ToString());
        Assert.Contains("Resume this work", resumed.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}
