namespace AgentSession.MCP.Interfaces;

public interface IFileSystem
{
    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    bool FileExists(string path);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken);

    Task WriteAllTextAtomicAsync(string path, string content, CancellationToken cancellationToken);

    IEnumerable<string> EnumerateDirectories(string path);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern);

    DateTimeOffset GetLastWriteTimeUtc(string path);

    string GetFileName(string path);
}
