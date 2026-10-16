using AgentSession.MCP.Core.Storage;

namespace AgentSession.MCP.Infrastructure.Configuration;

public static class StorageOptionsValidator
{
    public const string RootMessage =
        "SystemStorage:Root must be an absolute local directory below a volume root; network and device paths are unsupported.";

    public const string RepositoryMessage =
        "Repository:Id is required and must be a stable lowercase filesystem-safe identifier (letters, digits and hyphens).";

    public const string LimitsMessage =
        "Memory:Limits storage bounds must be positive and the compressed archive bound must cover the record bound.";

    public static bool ValidRoot(SystemStorageOptions options)
    {
        var root = options.Root;
        if (
            string.IsNullOrWhiteSpace(root)
            || root != root.Trim()
            || root.StartsWith("\\\\", StringComparison.Ordinal)
            || root.StartsWith("//", StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(root)
            || root.IndexOfAny(Path.GetInvalidPathChars()) >= 0
        )
            return false;

        try
        {
            var fullPath = Path.GetFullPath(root);
            if (Path.TrimEndingDirectorySeparator(fullPath) == Path.GetPathRoot(fullPath))
                return false;
            var relative = fullPath[Path.GetPathRoot(fullPath)!.Length..];
            return relative
                .Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries
                )
                .All(part => part.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public static bool ValidRepository(RepositoryOptions options) =>
        ManagedStorageIdentifier.IsValid(options.Id);

    public static bool ValidLimits(ManagedStorageOptions options) =>
        options.MaxRecordBytes > 0
        && options.MaxMutationBatch > 0
        && options.MaxRecallResults > 0
        && options.MaxArchiveCompressedBytes >= options.MaxRecordBytes;
}
