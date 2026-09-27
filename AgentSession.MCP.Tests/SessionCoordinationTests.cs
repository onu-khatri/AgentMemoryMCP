using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Extensions;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSession.MCP.Tests;

public sealed class SessionCoordinationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "coordination-tests", Guid.NewGuid().ToString("N"));
    private readonly ControlledTime _time = new();
    private ServiceProvider Build() => new ServiceCollection().AddSingleton<TimeProvider>(_time)
        .AddMemoryStorageConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["SystemStorage:Root"] = _root, ["Repository:Id"] = "repo" }).Build()).BuildServiceProvider();
    private static CoordinateTaskRequest Request(string operation, TaskAction action, long revision = 0, string actor = "agent-a", string? token = null)
        => new() { SessionId = "session", ActorId = actor, OperationId = operation, TaskId = "task",
            Action = action, ExpectedRevision = revision, ClaimToken = token, WorkKey = "implement-feature", Title = "Implement feature" };
    private static UpdateSessionArtifactsRequest Artifacts(string operation, string actor, long revision, string? token = null)
        => new() { SessionId = "session", ActorId = actor, OperationId = operation, Updates = [
            new(new SharedSessionArtifact { Id = "finding", Kind = SessionArtifactKind.Finding, ActorId = actor,
                TaskId = "task", Title = "Investigation", Data = JsonSerializer.SerializeToElement(new { conclusion = "Reuse parser" }) }, revision, token),
            new(new SharedSessionArtifact { Id = "handoff", Kind = SessionArtifactKind.Handoff, ActorId = actor,
                TaskId = "task", Title = "Next step", Data = JsonSerializer.SerializeToElement(new { next = "Add validation" }) }, revision, token)
        ] };

    [Fact]
    public async Task TwoProcessesCompeteForOneClaim()
    {
        using var provider = Build();
        await provider.GetRequiredService<SessionCoordinationService>().CoordinateAsync(Request("create", TaskAction.Create));
        using var first = new WorkerProcess("coordinate", _root);
        using var second = new WorkerProcess("coordinate", _root);
        Assert.Equal("ready", await first.ReadLineAsync());
        Assert.Equal("ready", await second.ReadLineAsync());
        var compact = new JsonSerializerOptions(MemoryJson.Options) { WriteIndented = false };
        await Task.WhenAll(
            first.Process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(Request("claim-a", TaskAction.Claim, 1), compact)),
            second.Process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(Request("claim-b", TaskAction.Claim, 1, "agent-b"), compact)));
        var results = await Task.WhenAll(first.ExitAsync(), second.ExitAsync());
        Assert.Equal(new[] { 0, 3 }, results.Order().ToArray());
    }

    [Fact]
    public async Task InterruptedArtifactBatchReturnsOriginalResultAfterRestart()
    {
        UpdateSessionArtifactsRequest request;
        using (var provider = Build())
        {
            var service = provider.GetRequiredService<SessionCoordinationService>();
            await service.CoordinateAsync(Request("create", TaskAction.Create));
            var claim = await service.CoordinateAsync(Request("claim", TaskAction.Claim, 1));
            request = Artifacts("interrupted", "agent-a", 0, claim.Task!.ClaimToken);
            provider.GetRequiredService<ManagedTransactionStore>().AfterMutation = index => { if (index == 0) throw new IOException("Injected interruption"); };
            await Assert.ThrowsAsync<IOException>(() => service.UpdateArtifactsAsync(request));
        }
        using var restarted = Build();
        var result = await restarted.GetRequiredService<SessionCoordinationService>().UpdateArtifactsAsync(request);
        Assert.Equal(4, result.Sequence);
        Assert.All(result.ArtifactRevisions.Values, revision => Assert.Equal(1, revision));
    }

    [Fact]
    public async Task ConcurrentDelegationWithSameWorkKeyCreatesOneTask()
    {
        using var first = new WorkerProcess("coordinate", _root);
        using var second = new WorkerProcess("coordinate", _root);
        Assert.Equal("ready", await first.ReadLineAsync());
        Assert.Equal("ready", await second.ReadLineAsync());
        var duplicate = Request("create-b", TaskAction.Create, actor: "agent-b");
        duplicate.TaskId = "second-id";
        var compact = new JsonSerializerOptions(MemoryJson.Options) { WriteIndented = false };
        await Task.WhenAll(
            first.Process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(Request("create-a", TaskAction.Create), compact)),
            second.Process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(duplicate, compact)));
        Assert.All(await Task.WhenAll(first.ExitAsync(), second.ExitAsync()), code => Assert.Equal(0, code));
        using var provider = Build();
        var resume = await provider.GetRequiredService<SessionResumeService>().ResumeAsync("session", "reader");
        Assert.Single(resume.Entries, entry => entry.Kind == "task");
    }

    [Fact]
    public async Task RestartPreservesLeaseAndTakeoverFencesPreviousOwner()
    {
        string token;
        using (var provider = Build())
        {
            var service = provider.GetRequiredService<SessionCoordinationService>();
            await service.CoordinateAsync(Request("create", TaskAction.Create));
            var claim = await service.CoordinateAsync(Request("claim", TaskAction.Claim, 1));
            token = claim.Task!.ClaimToken!;
            Assert.Equal(_time.GetUtcNow().AddMinutes(15), claim.Task.ClaimExpiresAtUtc);
            await service.UpdateArtifactsAsync(Artifacts("contribute", "agent-a", 0, token));
        }
        using var restarted = Build();
        var resumed = restarted.GetRequiredService<SessionCoordinationService>();
        await Assert.ThrowsAsync<ValidationException>(() => resumed.CoordinateAsync(Request("early", TaskAction.Claim, 2, "agent-b")));
        _time.Advance(TimeSpan.FromMinutes(15));
        var takeover = await resumed.CoordinateAsync(Request("takeover", TaskAction.Claim, 2, "agent-b"));
        Assert.NotEqual(token, takeover.Task!.ClaimToken);
        Assert.Equal(2, takeover.Task.FencingGeneration);
        await Assert.ThrowsAsync<ValidationException>(() => resumed.UpdateArtifactsAsync(Artifacts("late-contribution", "agent-a", 1, token)));
        var late = Request("late-complete", TaskAction.Complete, 3, token: token);
        late.Handoff = "Done";
        await Assert.ThrowsAsync<ValidationException>(() => resumed.CoordinateAsync(late));
        await resumed.UpdateArtifactsAsync(Artifacts("new-contribution", "agent-b", 1, takeover.Task.ClaimToken));
    }

    [Fact]
    public async Task ArtifactBatchRetriesOriginalResultAndConflictsCannotPartiallyCommit()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<SessionCoordinationService>();
        await service.CoordinateAsync(Request("create", TaskAction.Create));
        var claim = await service.CoordinateAsync(Request("claim", TaskAction.Claim, 1));
        var contribution = Artifacts("contribution", "agent-a", 0, claim.Task!.ClaimToken);
        var first = await service.UpdateArtifactsAsync(contribution);
        var retry = await service.UpdateArtifactsAsync(contribution);
        Assert.Equal(first.Sequence, retry.Sequence);
        Assert.Equal(0, contribution.Updates[0].Artifact.Revision);
        var invalid = Artifacts("conflict", "agent-a", 1, claim.Task.ClaimToken);
        invalid.Updates[1] = invalid.Updates[1] with { ExpectedRevision = 0 };
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateArtifactsAsync(invalid));
        var valid = await service.UpdateArtifactsAsync(Artifacts("valid", "agent-a", 1, claim.Task.ClaimToken));
        Assert.All(valid.ArtifactRevisions.Values, revision => Assert.Equal(2, revision));
        contribution.Updates[0].Artifact.Title = "Different request";
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateArtifactsAsync(contribution));
    }

    [Fact]
    public async Task CompletionRequiresEvidenceForVerificationAndReopenIsAudited()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<SessionCoordinationService>();
        await service.CoordinateAsync(Request("create", TaskAction.Create));
        var claim = await service.CoordinateAsync(Request("claim", TaskAction.Claim, 1));
        var complete = Request("complete", TaskAction.Complete, 2, token: claim.Task!.ClaimToken);
        complete.Handoff = "Parser implementation ready";
        complete.VerificationStatus = "passed";
        await Assert.ThrowsAsync<ValidationException>(() => service.CoordinateAsync(complete));
        complete.VerificationStatus = "unverified";
        var done = await service.CoordinateAsync(complete);
        Assert.Equal(WorkStatus.Completed, done.Task!.Status);
        Assert.Equal("unverified", done.Task.VerificationStatus);
        await Assert.ThrowsAsync<ValidationException>(() => service.CoordinateAsync(Request("implicit", TaskAction.Claim, 3)));
        var reopen = Request("reopen", TaskAction.Reopen, 3);
        reopen.Reason = "Additional acceptance criterion";
        await service.CoordinateAsync(reopen);
        var json = await provider.GetRequiredService<ManagedTransactionStore>().ReadAsync(new(ManagedArea.Session, "session", ["coordination", "metadata.json"]));
        var metadata = JsonSerializer.Deserialize<SessionCoordinationMetadata>(json!, MemoryJson.Options)!;
        Assert.Equal(reopen.Reason, metadata.Changes.Last().Reason);
    }

    [Fact]
    public async Task PaginationIsStableAndAcknowledgementSurvivesRestart()
    {
        long consumedSequence;
        using (var provider = Build())
        {
            var coordination = provider.GetRequiredService<SessionCoordinationService>();
            var resume = provider.GetRequiredService<SessionResumeService>();
            await coordination.CoordinateAsync(Request("create", TaskAction.Create));
            var first = await resume.ResumeAsync("session", "reader", 1);
            Assert.True(first.HasMore);
            Assert.Equal(0, first.AcknowledgedSequence);
            await Assert.ThrowsAsync<ValidationException>(() => resume.CheckpointAsync("session", "reader", first.SnapshotId, first.Sequence));
            await coordination.CoordinateAsync(Request("claim", TaskAction.Claim, 1));
            var last = await resume.ResumeAsync("session", "reader", 1, first.Cursor);
            Assert.False(last.HasMore);
            Assert.Equal(first.Sequence, last.Sequence);
            Assert.Equal("task-create", last.Entries.Single().Value.GetProperty("kind").GetString());
            await Assert.ThrowsAsync<ValidationException>(() => resume.ResumeAsync("session", "other-reader", 1, first.Cursor));
            var checkpoint = await resume.CheckpointAsync("session", "reader", last.SnapshotId, last.Sequence);
            consumedSequence = checkpoint.AcknowledgedSequence;
        }
        using var restarted = Build();
        var afterRestart = await restarted.GetRequiredService<SessionResumeService>().ResumeAsync("session", "reader");
        Assert.Equal(consumedSequence, afterRestart.AcknowledgedSequence);
        var change = Assert.Single(afterRestart.Entries, x => x.Kind == "change");
        Assert.Equal("task-claim", change.Value.GetProperty("kind").GetString());
        Assert.Equal(2, afterRestart.Sequence);
    }

    [Fact]
    public async Task ResumePagesSessionLargerThanSingleRecordLimitWithoutSkippingArtifacts()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<SessionCoordinationService>();
        for (var batch = 0; batch < 2; batch++)
        {
            var updates = Enumerable.Range(batch * 10, 10).Select(index => new SessionArtifactUpdate(new SharedSessionArtifact
            {
                Id = "finding-" + index, Kind = SessionArtifactKind.Finding, ActorId = "agent-a", Title = "Finding " + index,
                Data = JsonSerializer.SerializeToElement(new { conclusion = new string('x', 60_000) })
            }, 0)).ToList();
            await service.UpdateArtifactsAsync(new() { SessionId = "session", ActorId = "agent-a", OperationId = "batch-" + batch, Updates = updates });
        }
        var resume = provider.GetRequiredService<SessionResumeService>();
        var page = await resume.ResumeAsync("session", "reader", 100);
        Assert.True(page.HasMore);
        var ids = new List<string>();
        while (true)
        {
            ids.AddRange(page.Entries.Where(entry => entry.Kind == "artifact").Select(entry => entry.Value.GetProperty("id").GetString()!));
            if (!page.HasMore) break;
            page = await resume.ResumeAsync("session", "reader", 100, page.Cursor);
        }
        Assert.Equal(20, ids.Count);
        Assert.Equal(20, ids.Distinct().Count());
        Assert.Equal(20, (await resume.CheckpointAsync("session", "reader", page.SnapshotId, page.Sequence)).AcknowledgedSequence);
    }

    [Fact]
    public async Task DelegationDeduplicatesAndDependenciesRequireCompletion()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<SessionCoordinationService>();
        await service.CoordinateAsync(Request("create", TaskAction.Create));
        var duplicate = Request("duplicate", TaskAction.Create);
        duplicate.TaskId = "another-id";
        Assert.Equal("task", (await service.CoordinateAsync(duplicate)).Task!.Id);
        duplicate.OperationId = "mismatch"; duplicate.Title = "Different work";
        await Assert.ThrowsAsync<ValidationException>(() => service.CoordinateAsync(duplicate));
        var dependent = Request("dependent", TaskAction.Create);
        dependent.TaskId = "tests"; dependent.WorkKey = "test-feature"; dependent.Dependencies = ["task"];
        await service.CoordinateAsync(dependent);
        var claimDependent = Request("claim-dependent", TaskAction.Claim, 1);
        claimDependent.TaskId = "tests";
        await Assert.ThrowsAsync<ValidationException>(() => service.CoordinateAsync(claimDependent));
        var owner = await service.CoordinateAsync(Request("claim", TaskAction.Claim, 1));
        var renew = await service.CoordinateAsync(Request("renew", TaskAction.Renew, 2, token: owner.Task!.ClaimToken));
        Assert.Equal(owner.Task.ClaimToken, renew.Task!.ClaimToken);
        var completion = Request("complete", TaskAction.Complete, 3, token: renew.Task.ClaimToken);
        completion.Handoff = "Implementation ready for tests";
        await service.CoordinateAsync(completion);
        Assert.Equal(WorkStatus.InProgress, (await service.CoordinateAsync(claimDependent)).Task!.Status);
    }

    [Fact]
    public async Task BlockRetainsEvidenceAndReleasesOwnership()
    {
        using var provider = Build();
        var service = provider.GetRequiredService<SessionCoordinationService>();
        await service.CoordinateAsync(Request("create", TaskAction.Create));
        var claim = await service.CoordinateAsync(Request("claim", TaskAction.Claim, 1));
        var block = Request("block", TaskAction.Block, 2, token: claim.Task!.ClaimToken);
        block.Reason = "Upstream service unavailable"; block.Evidence = ["outage-report"];
        var result = await service.CoordinateAsync(block);
        Assert.Null(result.Task!.OwnerId);
        Assert.Equal(WorkStatus.Blocked, result.Task.Status);
        Assert.Equal(block.Evidence, result.Task.Evidence);
        var reopen = Request("reopen", TaskAction.Reopen, 3); reopen.Reason = "Service restored";
        await service.CoordinateAsync(reopen);
        var next = await service.CoordinateAsync(Request("claim-again", TaskAction.Claim, 4));
        await service.CoordinateAsync(Request("release", TaskAction.Release, 5, token: next.Task!.ClaimToken));
        Assert.Equal(WorkStatus.InProgress, (await service.CoordinateAsync(Request("other-owner", TaskAction.Claim, 6, "agent-b"))).Task!.Status);
    }

    [Fact]
    public async Task ExpiredOrForgedCursorRequiresResyncAndReadDoesNotCheckpoint()
    {
        using var provider = Build();
        await provider.GetRequiredService<SessionCoordinationService>().CoordinateAsync(Request("create", TaskAction.Create));
        var resume = provider.GetRequiredService<SessionResumeService>();
        var first = await resume.ResumeAsync("session", "reader", 1);
        await Assert.ThrowsAsync<ValidationException>(() => resume.ResumeAsync("session", "reader", 1, first.SnapshotId + ":2"));
        _time.Advance(TimeSpan.FromMinutes(60));
        var expired = await Assert.ThrowsAsync<ValidationException>(() => resume.ResumeAsync("session", "reader", 1, first.Cursor));
        Assert.Contains("resynchronization", expired.Message);
        await Assert.ThrowsAsync<ValidationException>(() => resume.CheckpointAsync("session", "reader", first.SnapshotId, first.Sequence));
        var fresh = await resume.ResumeAsync("session", "reader");
        Assert.Equal(0, fresh.AcknowledgedSequence);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private sealed class ControlledTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
