using AgentSession.MCP.Extensions;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSession.MCP.Tests;

public sealed class ManagedTransactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "memory-transaction-tests", Guid.NewGuid().ToString("N"));
    private ServiceProvider Build() => new ServiceCollection().AddMemoryStorageConfiguration(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["SystemStorage:Root"] = _root, ["Repository:Id"] = "repo" }).Build()).BuildServiceProvider();
    private static ManagedFile FileFor(string id) => new(ManagedArea.Session, "session", ["coordination", id + ".json"]);

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
