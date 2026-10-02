using System.Diagnostics;
using AgentSession.MCP.Extensions;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Observability;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace AgentSession.MCP.Tests;

public sealed class ManagedTransactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "memory-transaction-tests", Guid.NewGuid().ToString("N"));
    private ServiceProvider Build() => new ServiceCollection().AddMemoryStorageConfiguration(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["SystemStorage:Root"] = _root, ["Repository:Id"] = "repo" }).Build()).BuildServiceProvider();
    private static ManagedFile FileFor(string id) => new(ManagedArea.Session, "session", ["coordination", id + ".json"]);

    [Fact]
    public async Task LockCommitAndRecoverySpansAreCoarseBoundedAndContentFree()
    {
        var activities = new ThreadSafeCollection<Activity>();
        using var tracing = Sdk.CreateTracerProviderBuilder()
            .AddSource(TelemetrySchema.ActivitySourceName)
            .AddInMemoryExporter(activities)
            .Build();
        using var testActivity = TelemetrySchema.Activities.StartActivity(
            "managed transaction telemetry test",
            ActivityKind.Internal
        );
        Assert.NotNull(testActivity);
        var testTraceId = testActivity.TraceId;
        const string operationCanary = "private-operation-canary";
        const string contentCanary = "private-content-canary";
        using (var provider = Build())
        {
            var store = provider.GetRequiredService<ManagedTransactionStore>();
            store.AfterDurableBoundary = boundary =>
            {
                if (boundary == "intent")
                    throw new IOException("private-exception-canary");
            };
            await Assert.ThrowsAsync<IOException>(() => store.CommitAsync(
                operationCanary,
                [
                    new(FileFor("first-private-file"), contentCanary, null),
                    new(FileFor("second-private-file"), "safe", null),
                ]
            ));
        }
        using (var restarted = Build())
        {
            var store = restarted.GetRequiredService<ManagedTransactionStore>();
            Assert.Equal(contentCanary, await store.ReadAsync(FileFor("first-private-file")));
        }
        Assert.True(tracing.ForceFlush(5_000));

        var filesystem = activities.Where(activity =>
            activity.TraceId == testTraceId
            && Equals(activity.GetTagItem("dependency.type"), "filesystem")).ToArray();
        Assert.Contains(filesystem, activity =>
            activity.OperationName == "mcp operation filesystem lock"
            && activity.Kind == ActivityKind.Internal);
        var transaction = Assert.Single(filesystem, activity =>
            activity.OperationName == "mcp operation filesystem transaction");
        Assert.Equal(2, transaction.GetTagItem("mcp.batch.items"));
        Assert.Equal("tool_error", transaction.GetTagItem("mcp.status"));
        Assert.Equal("storage_error", transaction.GetTagItem("error.type"));
        Assert.Contains(filesystem, activity =>
            activity.OperationName == "mcp operation filesystem recovery"
            && Equals(activity.GetTagItem("mcp.recovery.performed"), true)
            && Equals(activity.GetTagItem("mcp.batch.items"), 1));
        var exported = string.Join(
            "|",
            filesystem.SelectMany(activity => activity.TagObjects).Select(tag => tag.Value)
        );
        foreach (var canary in new[]
        {
            operationCanary,
            contentCanary,
            "private-exception-canary",
            "first-private-file",
            _root,
        })
            Assert.DoesNotContain(canary, exported, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestartRollsForwardPartialCommitBeforeReading()
    {
        using (var provider = Build())
        {
            var store = provider.GetRequiredService<ManagedTransactionStore>();
            store.AfterMutation = index => { if (index == 0) throw new IOException("Injected process failure"); };
            await Assert.ThrowsAsync<IOException>(() => store.CommitAsync("operation", [
                new(FileFor("finding"), "first", null), new(FileFor("handoff"), "second", null)]));
        }
        using var restarted = Build();
        var recovered = restarted.GetRequiredService<ManagedTransactionStore>();
        Assert.Equal("second", await recovered.ReadAsync(FileFor("handoff")));
        Assert.Equal("first", await recovered.ReadAsync(FileFor("finding")));
        await recovered.CommitAsync("operation", [new(FileFor("finding"), "first", null), new(FileFor("handoff"), "second", null)]);
        Assert.Empty(Directory.GetFiles(_root, "*.intent.json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CancellationBeforeIntentDoesNotMutateRecords()
    {
        using var provider = Build();
        var store = provider.GetRequiredService<ManagedTransactionStore>();
        await store.CommitAsync("original", [new(FileFor("finding"), "original", null)]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CommitAsync("canceled", [
            new(FileFor("finding"), "replacement", ManagedTransactionStore.Hash("original"))], cancellation.Token));
        Assert.Equal("original", await store.ReadAsync(FileFor("finding")));
        Assert.Empty(Directory.GetFiles(_root, "*.intent.json", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("intent")]
    [InlineData("mutation-0")]
    [InlineData("mutation-1")]
    [InlineData("receipt")]
    [InlineData("complete")]
    public async Task EveryDurableBoundaryRecoversOneCommit(string boundary)
    {
        ManagedMutation[] changes = [new(FileFor("finding"), "finding", null), new(FileFor("handoff"), "handoff", null)];
        using (var provider = Build())
        {
            var store = provider.GetRequiredService<ManagedTransactionStore>();
            store.AfterDurableBoundary = name => { if (name == boundary) throw new IOException("Simulated interruption"); };
            await Assert.ThrowsAsync<IOException>(() => store.CommitAsync("interrupted", changes));
        }
        using var restarted = Build();
        var recovered = restarted.GetRequiredService<ManagedTransactionStore>();
        var snapshot = await recovered.ReadManyAsync([FileFor("finding"), FileFor("handoff")]);
        Assert.Equal(new[] { "finding", "handoff" }, snapshot);
        await recovered.CommitAsync("interrupted", changes);
        Assert.Single(Directory.GetFiles(_root, "*.receipt.json", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(_root, "*.intent.json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task StaleRevisionAndReusedOperationDoNotOverwrite()
    {
        using var provider = Build();
        var store = provider.GetRequiredService<ManagedTransactionStore>();
        await store.CommitAsync("create", [new(FileFor("finding"), "original", null)]);
        await Assert.ThrowsAsync<ValidationException>(() => store.CommitAsync("other", [new(FileFor("finding"), "replacement", null)]));
        await Assert.ThrowsAsync<ValidationException>(() => store.CommitAsync("create", [new(FileFor("finding"), "different", null)]));
        Assert.Equal("original", await store.ReadAsync(FileFor("finding")));
        await store.CommitAsync("update", [new(FileFor("finding"), "replacement", ManagedTransactionStore.Hash("original"))]);
        Assert.Equal("replacement", await store.ReadAsync(FileFor("finding")));
    }

    [Fact]
    public async Task SecretNeverReachesStagingAndReservedPathsFail()
    {
        using var provider = Build();
        var store = provider.GetRequiredService<ManagedTransactionStore>();
        await Assert.ThrowsAsync<ValidationException>(() => store.CommitAsync("secret", [new(FileFor("finding"), "password=synthetic-value", null)]));
        Assert.False(Directory.Exists(_root));
        await Assert.ThrowsAsync<ValidationException>(() => store.CommitAsync("reserved", [new(new ManagedFile(ManagedArea.Learning, null, [".operations", "evil.json"]), "text", null)]));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
