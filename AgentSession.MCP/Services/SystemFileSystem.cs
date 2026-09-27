using System.Text;

namespace AgentSession.MCP.Services;

public sealed class SystemFileSystem
{
    public Task WriteAllTextAtomicAsync(
        string path,
        string content,
        CancellationToken cancellationToken
    )
    {
        // Throw on malformed UTF-16 input instead of silently persisting replacement characters.
        var bytes = new UTF8Encoding(false, true).GetBytes(content);
        return WriteAtomicAsync(
            path,
            (stream, token) => stream.WriteAsync(bytes, token).AsTask(),
            cancellationToken
        );
    }

    public Task WriteAllBytesAtomicAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken
    ) =>
        WriteAtomicAsync(
            path,
            (stream, token) => stream.WriteAsync(content, token).AsTask(),
            cancellationToken
        );

    public async Task WriteAtomicAsync(
        string path,
        Func<Stream, CancellationToken, Task> write,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory =
            Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Target path has no directory.");

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (
                var stream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous | FileOptions.WriteThrough
                )
            )
            {
                await write(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
                File.Replace(tempPath, path, null, ignoreMetadataErrors: false);
            else
                File.Move(tempPath, path);
        }
        finally
        {
            // After commit the staging name no longer exists. Failed staging must not accumulate.
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
