using AgentSession.MCP.Observability;

namespace AgentSession.MCP.Services;

/// <summary>One bounded in-process gate and one OS file lease per bound repository.</summary>
public sealed class RepositoryMutationLock(
    ManagedStoragePathResolver paths,
    McpDependencyTelemetry telemetry
) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task<IAsyncDisposable> AcquireAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken
    ) => telemetry.TrackInternalAsync(
        "filesystem",
        "lock",
        null,
        token => AcquireCoreAsync(timeout, token),
        cancellationToken
    );

    private async Task<IAsyncDisposable> AcquireCoreAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var entered = false;
        try
        {
            await _gate.WaitAsync(deadline.Token);
            entered = true;
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var path = paths.RepositoryLockFile();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                path = paths.RepositoryLockFile();
                try
                {
                    var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    return new Lease(file, _gate);
                }
                catch (IOException ex) when (IsSharingConflict(ex))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50), deadline.Token);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (entered) _gate.Release();
            throw new TimeoutException("Timed out waiting for the repository mutation lock.");
        }
        catch
        {
            if (entered) _gate.Release();
            throw;
        }
    }

    private static bool IsSharingConflict(IOException exception)
        => (exception.HResult & 0xffff) is 32 or 33 or 11;

    public void Dispose() => _gate.Dispose();

    private sealed class Lease(FileStream file, SemaphoreSlim gate) : IAsyncDisposable
    {
        private FileStream? _file = file;
        public ValueTask DisposeAsync()
        {
            var current = Interlocked.Exchange(ref _file, null);
            if (current is not null)
            {
                current.Dispose();
                gate.Release();
            }
            return ValueTask.CompletedTask;
        }
    }
}
