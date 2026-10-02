namespace AgentSession.MCP.Services;

internal sealed class EofSignalingReadStream(Stream inner) : Stream
{
    private Action? _onEndOfStream;
    private int _signaled;

    public Action? OnEndOfStream
    {
        set => _onEndOfStream = value;
    }

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => inner.Length;
    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override void Flush() => inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) =>
        SignalIfEof(inner.Read(buffer, offset, count));

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => SignalIfEof(await inner.ReadAsync(buffer, cancellationToken));

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => SignalIfEof(await inner.ReadAsync(buffer, offset, count, cancellationToken));

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private int SignalIfEof(int bytesRead)
    {
        if (bytesRead == 0 && Interlocked.Exchange(ref _signaled, 1) == 0)
            _onEndOfStream?.Invoke();
        return bytesRead;
    }
}
