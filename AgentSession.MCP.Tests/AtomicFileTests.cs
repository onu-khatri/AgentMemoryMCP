using System.Text;
using AgentSession.MCP.Services;

namespace AgentSession.MCP.Tests;

public sealed class AtomicFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "atomic-memory-tests", Guid.NewGuid().ToString("N"));
    private readonly SystemFileSystem _files = new();

    [Fact]
    public async Task ReplacementIsCompleteUtf8WithoutBom()
    {
        var path = Path.Combine(_root, "record.json");
        await _files.WriteAllTextAtomicAsync(path, "original", default);
        await _files.WriteAllTextAtomicAsync(path, "नमस्ते 🌍", default);
        Assert.Equal(new UTF8Encoding(false, true).GetBytes("नमस्ते 🌍"), await File.ReadAllBytesAsync(path));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task FailureAfterPartialStagingPreservesOriginalAndRemovesStage()
    {
        var path = Path.Combine(_root, "record.json");
        await _files.WriteAllTextAtomicAsync(path, "original", default);
        await Assert.ThrowsAsync<IOException>(() => _files.WriteAtomicAsync(path, async (stream, token) =>
        {
            await stream.WriteAsync(new byte[] { 1, 2, 3 }, token);
            throw new IOException("Injected write failure");
        }, default));
        Assert.Equal("original", await File.ReadAllTextAsync(path));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task CancellationBeforeCommitPreservesOriginal()
    {
        var path = Path.Combine(_root, "record.json");
        await _files.WriteAllTextAtomicAsync(path, "original", default);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _files.WriteAtomicAsync(path,
            async (stream, token) =>
            {
                await stream.WriteAsync(new byte[] { 1, 2, 3 }, token);
                cancellation.Cancel();
            }, cancellation.Token));
        Assert.Equal("original", await File.ReadAllTextAsync(path));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task InvalidUnicodeNeverCreatesFile()
    {
        var path = Path.Combine(_root, "record.json");
        await Assert.ThrowsAsync<EncoderFallbackException>(() => _files.WriteAllTextAtomicAsync(path, "\ud800", default));
        Assert.False(File.Exists(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
