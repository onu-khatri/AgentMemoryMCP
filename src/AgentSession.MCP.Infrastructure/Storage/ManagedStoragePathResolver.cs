using AgentSession.MCP.Core.Storage;
using AgentSession.MCP.Helpers;

namespace AgentSession.MCP.Services;

public sealed class ManagedStoragePathResolver(ManagedStoragePaths paths) : IManagedStoragePathResolver
{
    public string SessionFile(string repositoryId, string sessionId, params string[] relativeParts)
    {
        RequireRepository(repositoryId);
        ManagedStorageIdentifier.RequireValid(sessionId);
        return Resolve(paths.SessionsRoot, [sessionId, .. relativeParts]);
    }

    public string LearningFile(string repositoryId, params string[] relativeParts)
    {
        RequireRepository(repositoryId);
        return Resolve(paths.LearningRoot, relativeParts);
    }

    public string RepositoryLockFile() => Resolve(paths.LocksRoot, [paths.RepositoryId + ".lock"]);

    public string ApprovalFile(string memoryId, long revision, string action)
    {
        ManagedStorageIdentifier.RequireValid(memoryId);
        ManagedStorageIdentifier.RequireValid(action);
        if (revision < 1)
            throw new ValidationException("Grant revision must be positive.");
        return Resolve(
            Path.Combine(paths.Root, ".authorizations", paths.RepositoryId),
            [memoryId + "-" + revision + "-" + action + ".json"]
        );
    }

    private void RequireRepository(string repositoryId)
    {
        if (!string.Equals(repositoryId, paths.RepositoryId, StringComparison.Ordinal))
            throw new ValidationException(
                "Repository identity does not match this server instance."
            );
    }

    private static string Resolve(string boundary, string[] parts)
    {
        if (parts.Length == 0)
            throw new ValidationException("A managed file path is required.");
        foreach (var part in parts)
        {
            if (
                string.IsNullOrWhiteSpace(part)
                || part is "." or ".."
                || part != part.Trim()
                || part.EndsWith('.')
                || part.IndexOfAny(['/', '\\', ':', '\0']) >= 0
                || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            )
                throw new ValidationException("Invalid managed path component.");
            var stem = part.Split('.')[0];
            if (stem.Length > 0 && !ManagedStorageIdentifier.IsValid(stem))
                throw new ValidationException("Invalid managed file name.");
        }
        var fullBoundary = Path.GetFullPath(boundary);
        var result = Path.GetFullPath(Path.Combine([fullBoundary, .. parts]));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (
            !result.StartsWith(
                Path.TrimEndingDirectorySeparator(fullBoundary) + Path.DirectorySeparatorChar,
                comparison
            )
        )
            throw new ValidationException("Managed path escapes its storage boundary.");
        RejectLinks(result);
        return result;
    }

    private static void RejectLinks(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new ValidationException(
                        "Links and reparse points are not allowed in managed storage paths."
                    );
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}
