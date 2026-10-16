using System.Diagnostics;
using AgentSession.MCP.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSession.MCP.IntegrationTests;

public sealed class RepositoryMutationLockTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "memory-lock-tests",
        Guid.NewGuid().ToString("N")
    );

    private ServiceProvider Build() => InfrastructureTestServices.Build(_root);

    [Fact]
    public async Task SeparateInstancesContendOnSameOsLockAndRecoverAfterTimeout()
    {
        using var first = Build();
        using var second = Build();
        var firstLock = first.GetRequiredService<RepositoryMutationLock>();
        var secondLock = second.GetRequiredService<RepositoryMutationLock>();
        var lease = await firstLock.AcquireAsync(TimeSpan.FromSeconds(5), default);
        await Assert.ThrowsAsync<TimeoutException>(() =>
            secondLock.AcquireAsync(TimeSpan.FromMilliseconds(100), default)
        );
        await lease.DisposeAsync();
        await using var next = await secondLock.AcquireAsync(TimeSpan.FromSeconds(5), default);
    }

    [Fact]
    public async Task AnotherProcessHoldsLeaseAndAbruptExitReleasesIt()
    {
        using var services = Build();
        var gate = services.GetRequiredService<RepositoryMutationLock>();
        using (var worker = new WorkerProcess("lock", _root))
        {
            Assert.Equal("locked", await worker.ReadLineAsync());
            await Assert.ThrowsAsync<TimeoutException>(() =>
                gate.AcquireAsync(TimeSpan.FromMilliseconds(150), default)
            );
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
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gate.AcquireAsync(TimeSpan.FromSeconds(5), cancellation.Token)
        );
        await lease.DisposeAsync();
        await lease.DisposeAsync();
        await using var next = await gate.AcquireAsync(TimeSpan.FromSeconds(5), default);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}

internal sealed class WorkerProcess : IDisposable
{
    public WorkerProcess(string command, string root)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(
            Path.Combine(AppContext.BaseDirectory, "worker", "AgentSession.MCP.TestWorker.dll")
        );
        start.ArgumentList.Add(command);
        start.ArgumentList.Add(root);
        Process = Process.Start(start)!;
    }

    public Process Process { get; }

    public async Task<string?> ReadLineAsync() =>
        await Process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));

    public async Task<int> ExitAsync()
    {
        await Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        return Process.ExitCode;
    }

    public void Dispose()
    {
        if (!Process.HasExited)
        {
            Process.Kill(entireProcessTree: true);
            Process.WaitForExit(5000);
        }
        Process.Dispose();
    }
}
