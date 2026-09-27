using AgentSession.MCP.Extensions;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSession.MCP.Tests;

public sealed class RepositoryMutationLockTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "memory-lock-tests", Guid.NewGuid().ToString("N"));
    private ServiceProvider Build() => new ServiceCollection().AddMemoryStorageConfiguration(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["SystemStorage:Root"] = _root, ["Repository:Id"] = "repo" }).Build()).BuildServiceProvider();

    [Fact]
    public async Task SeparateInstancesContendOnSameOsLockAndRecoverAfterTimeout()
    {
        using var first = Build();
        using var second = Build();
        var a = first.GetRequiredService<RepositoryMutationLock>();
        var b = second.GetRequiredService<RepositoryMutationLock>();
        var lease = await a.AcquireAsync(TimeSpan.FromSeconds(5), default);
        await Assert.ThrowsAsync<TimeoutException>(() => b.AcquireAsync(TimeSpan.FromMilliseconds(100), default));
        await lease.DisposeAsync();
        await using var next = await b.AcquireAsync(TimeSpan.FromSeconds(5), default);
    }

    [Fact]
    public async Task AnotherProcessHoldsLeaseAndAbruptExitReleasesIt()
    {
        using var services = Build();
        var gate = services.GetRequiredService<RepositoryMutationLock>();
        using (var worker = new WorkerProcess("lock", _root))
        {
            Assert.Equal("locked", await worker.ReadLineAsync());
            await Assert.ThrowsAsync<TimeoutException>(() => gate.AcquireAsync(TimeSpan.FromMilliseconds(150), default));
            worker.Process.Kill(entireProcessTree: true);
            await worker.ExitAsync();
        }
        await using var recovered = await gate.AcquireAsync(TimeSpan.FromSeconds(5), default);
    }

    [Fact]
    public async Task CancellationIsNotConvertedToTimeoutAndDoubleDisposeIsSafe()
    {
        using var services = Build();
        var gate = services.GetRequiredService<RepositoryMutationLock>();
        var lease = await gate.AcquireAsync(TimeSpan.FromSeconds(5), default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.AcquireAsync(TimeSpan.FromSeconds(5), cancellation.Token));
        await lease.DisposeAsync();
        await lease.DisposeAsync();
        await using var next = await gate.AcquireAsync(TimeSpan.FromSeconds(5), default);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
