using System.Net;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Options;
using AgentSession.MCP.Observability;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qdrant.Client;

namespace AgentSession.MCP.Extensions;

public static class MemoryConfigurationExtensions
{
    public static IServiceCollection AddMemoryStorageConfiguration(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<SystemStorageOptions>()
            .Bind(configuration.GetSection(SystemStorageOptions.SectionName))
            .Validate(
                IsLocalAbsoluteRoot,
                "SystemStorage:Root must be an absolute local directory below a volume root; network and device paths are unsupported."
            )
            .ValidateOnStart();
        services
            .AddOptions<RepositoryOptions>()
            .Bind(configuration.GetSection(RepositoryOptions.SectionName))
            .Validate(
                options => NameSanitizer.IsSafePathSegment(options.Id),
                "Repository:Id is required and must be a stable lowercase filesystem-safe identifier (letters, digits and hyphens)."
            )
            .ValidateOnStart();
        services.TryAddSingleton<MemoryStoragePaths>();
        services.TryAddSingleton<OnuObservability.Mcp.IMcpDependencyFailureRecorder,
            NoOpMcpDependencyFailureRecorder>();
        services.TryAddSingleton<McpDependencyTelemetry>();
        services.TryAddSingleton<ManagedStoragePathResolver>();
        services.TryAddSingleton<RepositoryMutationLock>();
        services.TryAddSingleton<IMemoryContentPolicy, MemoryContentPolicy>();
        services.TryAddSingleton<ManagedTransactionStore>();
        services.TryAddSingleton<MemoryRequestValidator>();
        services.TryAddSingleton<LearningCatalog>();
        services.TryAddSingleton<CanonicalMemoryService>();
        services.TryAddSingleton<OperatorGrantVerifier>();
        services.TryAddSingleton<MemoryMaintenanceState>();
        services.TryAddSingleton<IMemoryMaintenancePass, MemoryMaintenancePass>();
        services.TryAddSingleton<SessionCoordinationService>();
        services.TryAddSingleton<SessionResumeService>();
        services.TryAddSingleton<SharedSessionLifecycleService>();
        services.TryAddSingleton(TimeProvider.System);
        services
            .AddHttpClient<OllamaEmbeddingClient>(
                (provider, client) =>
                {
                    var embedding = provider
                        .GetRequiredService<Microsoft.Extensions.Options.IOptions<EmbeddingOptions>>()
                        .Value;
                    client.BaseAddress = new Uri(embedding.Ollama.BaseUrl, UriKind.Absolute);
                    client.Timeout = Timeout.InfiniteTimeSpan;
                }
            )
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }
            );
        services.TryAddSingleton(provider =>
        {
            var qdrant = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantOptions>>()
                .Value;
            return new QdrantClient(qdrant.Host, qdrant.GrpcPort);
        });
        services.TryAddSingleton<QdrantVectorIndex>();
        services.TryAddSingleton<VectorIndexCoordinator>();
        services.TryAddSingleton<VectorMigrationService>();
        services.TryAddSingleton<MemoryReindexService>();
        services.TryAddSingleton<MemoryStatusService>();
        services
            .AddOptions<SessionCoordinationOptions>()
            .Bind(configuration.GetSection("SessionCoordination"))
            .Validate(
                options =>
                    PositiveDuration(options.ClaimLeaseMinutes, 60)
                    && PositiveDuration(options.SnapshotLifetimeMinutes, 60),
                "SessionCoordination lease and snapshot durations must be finite and positive."
            )
            .ValidateOnStart();
        services
            .AddOptions<MemoryPolicyOptions>()
            .Bind(configuration.GetSection("Memory"))
            .Validate(
                ValidPolicy,
                "Memory policies must have finite positive limits, a temp lifetime at most 24 hours, gzip archives and valid recall bounds."
            )
            .ValidateOnStart();
        services
            .AddOptions<EmbeddingOptions>()
            .Bind(configuration.GetSection("Embedding"))
            .Validate(
                options =>
                    options.Provider == "ollama"
                    && PositiveDuration(options.TimeoutSeconds, 1)
                    && options.MaxBatchSize is > 0 and <= 1024
                    && options.MaxInputBytes > 0
                    && options.Ollama is not null
                    && !string.IsNullOrWhiteSpace(options.Ollama.Model)
                    && IsLoopbackUrl(options.Ollama.BaseUrl),
                "Embedding requires ollama, a positive finite timeout, model and loopback HTTP(S) base URL without credentials, query or fragment."
            )
            .ValidateOnStart();
        services
            .AddOptions<QdrantOptions>()
            .Bind(configuration.GetSection("Qdrant"))
            .Validate(
                options =>
                    IsLoopbackHost(options.Host)
                    && options.GrpcPort is > 0 and <= 65535
                    && options.RestPort is > 0 and <= 65535
                    && !string.IsNullOrWhiteSpace(options.Collection)
                    && options.Collection.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'),
                "Qdrant requires a loopback host, valid ports and a collection prefix containing letters, digits, hyphens or underscores."
            )
            .ValidateOnStart();
        return services;
    }

    private static bool PositiveDuration(double value, double secondsPerUnit) =>
        double.IsFinite(value)
        && value > 0
        && value <= TimeSpan.MaxValue.TotalSeconds / secondsPerUnit;

    private static bool ValidPolicy(MemoryPolicyOptions options)
    {
        if (
            options.Temp is not { } temp
            || options.Short is not { } shortTerm
            || options.Long is not { } longTerm
            || options.Limits is not { } limits
        )
            return false;
        return PositiveDuration(temp.MaxAgeHours, 3600)
            && temp.MaxAgeHours <= 24
            && PositiveDuration(temp.CleanupIntervalMinutes, 60)
            && temp.MaxEntriesPerSession > 0
            && PositiveDuration(shortTerm.ReviewAfterDays, 86400)
            && PositiveDuration(shortTerm.CompactionCandidateAfterDays, 86400)
            && PositiveDuration(shortTerm.ArchiveAfterDays, 86400)
            && shortTerm.MaxActiveRecords > 0
            && shortTerm.ArchiveCompression == "gzip"
            && longTerm.RequiredIndependentConfirmations > 0
            && (
                longTerm.MinimumSimilarity is null
                || (
                    double.IsFinite(longTerm.MinimumSimilarity.Value)
                    && longTerm.MinimumSimilarity is >= -1 and <= 1
                )
            )
            && longTerm.DefaultMaxResults > 0
            && longTerm.DefaultMaxResults <= limits.MaxRecallResults
            && limits.MaxContentBytes > 0
            && limits.MaxRecordBytes >= limits.MaxContentBytes
            && limits.MaxJsonDepth > 0
            && limits.MaxMutationBatch > 0
            && limits.MaxRecallResults > 0
            && limits.MaxEventJournalBytes >= 256
            && limits.MaxEventJournalBytes <= limits.MaxRecordBytes
            && limits.MaxArchiveCompressedBytes >= limits.MaxRecordBytes
            && limits.MaxArchiveExpandedBytes >= limits.MaxArchiveCompressedBytes;
    }

    private static bool IsLoopbackHost(string? host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));

    private static bool IsLoopbackUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && IsLoopbackHost(uri.DnsSafeHost)
        && uri.UserInfo.Length == 0
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0
        && uri.AbsolutePath == "/";

    private static bool IsLocalAbsoluteRoot(SystemStorageOptions options)
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
            // ADS and invalid filename characters are not directory components.
            var relative = fullPath[Path.GetPathRoot(fullPath)!.Length..];
            return relative
                .Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries
                )
                .All(part => part.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
        }
        catch (Exception ex)
            when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
