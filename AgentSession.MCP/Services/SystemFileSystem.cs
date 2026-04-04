using AgentSession.MCP.Interfaces;

namespace AgentSession.MCP.Services;

public sealed class SystemFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public bool FileExists(string path) => File.Exists(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
        => File.ReadAllTextAsync(path, cancellationToken);

    public async Task WriteAllTextAtomicAsync(string path, string content, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Target path has no directory.");

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(tempPath, content, cancellationToken);

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null, ignoreMetadataErrors: true);
            return;
        }

        File.Move(tempPath, path);
    }

    public IEnumerable<string> EnumerateDirectories(string path)
        => !Directory.Exists(path) ? [] : Directory.EnumerateDirectories(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        => !Directory.Exists(path) ? [] : Directory.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly);

    public DateTimeOffset GetLastWriteTimeUtc(string path)
    {
        if (Directory.Exists(path))
        {
            return Directory.GetLastWriteTimeUtc(path);
        }

        if (File.Exists(path))
        {
            return File.GetLastWriteTimeUtc(path);
        }

        return DateTimeOffset.MinValue;
    }

    public string GetFileName(string path) => Path.GetFileName(path);
}
