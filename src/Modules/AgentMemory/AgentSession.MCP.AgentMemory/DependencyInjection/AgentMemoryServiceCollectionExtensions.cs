using System.Net;
using AgentSession.MCP.AgentMemory.Capabilities;
using AgentSession.MCP.AgentMemory.Contracts.Capabilities;
using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Options;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSession.MCP.AgentMemory.DependencyInjection;

public static class AgentMemoryServiceCollectionExtensions
{
    public static IServiceCollection AddAgentMemoryModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<MemoryPolicyOptions>()
            .Bind(configuration.GetSection("Memory"))
            .Validate(ValidPolicy, "Memory policies are invalid.")
            .ValidateOnStart();
        services.AddOptions<EmbeddingOptions>()
            .Bind(configuration.GetSection("Embedding"))
            .Validate(ValidEmbedding, "Embedding configuration is invalid.")
            .ValidateOnStart();
        services.AddOptions<QdrantOptions>()
            .Bind(configuration.GetSection("Qdrant"))
            .Validate(ValidQdrant, "Qdrant configuration is invalid.")
            .ValidateOnStart();

        services.TryAddSingleton<MemoryContentPolicy>();
        services.TryAddSingleton<IMemoryContentPolicy>(p => p.GetRequiredService<MemoryContentPolicy>());
        services.TryAddSingleton<IManagedMutationPolicy>(p => p.GetRequiredService<MemoryContentPolicy>());
        services.TryAddSingleton<MemoryRequestValidator>();
        services.TryAddSingleton<LearningCatalog>();
        services.TryAddSingleton<CanonicalMemoryService>();
        services.TryAddSingleton<OperatorGrantVerifier>();
        services.TryAddSingleton<MemoryMaintenanceState>();
        services.TryAddSingleton<IMemoryMaintenancePass, MemoryMaintenancePass>();
        services.AddHostedService<MemoryMaintenanceService>();
        services.TryAddSingleton<MemoryReindexService>();
        services.TryAddSingleton<MemoryStatusService>();
        services.TryAddSingleton<VectorIndexCoordinator>();
        services.TryAddSingleton<VectorMigrationService>();
        services.TryAddSingleton<QdrantVectorIndex>();
        services.AddHttpClient<OllamaEmbeddingClient>((provider, client) =>
        {
            client.BaseAddress = new Uri(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmbeddingOptions>>().Value.Ollama.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
        services.TryAddSingleton(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantOptions>>().Value;
            return new Qdrant.Client.QdrantClient(options.Host, options.GrpcPort);
        });

        services.TryAddSingleton<MemoryQueryCapabilities>();
        services.TryAddSingleton<IMemoryQueryCapabilities>(p => p.GetRequiredService<MemoryQueryCapabilities>());
        services.TryAddSingleton<MemoryCommandCapabilities>();
        services.TryAddSingleton<IMemoryCommandCapabilities>(p => p.GetRequiredService<MemoryCommandCapabilities>());
        services.TryAddSingleton<MemoryIndexCapabilities>();
        services.TryAddSingleton<IMemoryIndexCapabilities>(p => p.GetRequiredService<MemoryIndexCapabilities>());
        return services;
    }

    private static bool ValidEmbedding(EmbeddingOptions options) =>
        options.Provider == "ollama" && Positive(options.TimeoutSeconds) && options.MaxBatchSize is > 0 and <= 1024
        && options.MaxInputBytes > 0 && options.Ollama is not null && !string.IsNullOrWhiteSpace(options.Ollama.Model)
        && Uri.TryCreate(options.Ollama.BaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" && IsLoopback(uri.DnsSafeHost)
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.AbsolutePath == "/";

    private static bool ValidQdrant(QdrantOptions options) =>
        IsLoopback(options.Host) && options.GrpcPort is > 0 and <= 65535 && options.RestPort is > 0 and <= 65535
        && !string.IsNullOrWhiteSpace(options.Collection) && options.Collection.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    private static bool ValidPolicy(MemoryPolicyOptions options) =>
        options.Temp is not null && options.Short is not null && options.Long is not null && options.Limits is not null
        && Positive(options.Temp.MaxAgeHours) && options.Temp.MaxAgeHours <= 24
        && Positive(options.Temp.CleanupIntervalMinutes) && options.Temp.MaxEntriesPerSession > 0
        && Positive(options.Short.ReviewAfterDays) && Positive(options.Short.CompactionCandidateAfterDays)
        && Positive(options.Short.ArchiveAfterDays) && options.Short.MaxActiveRecords > 0
        && options.Short.ArchiveCompression == "gzip" && options.Long.RequiredIndependentConfirmations > 0
        && options.Long.MinimumSimilarity is null or (>= -1 and <= 1)
        && options.Long.DefaultMaxResults > 0 && options.Long.DefaultMaxResults <= options.Limits.MaxRecallResults
        && options.Limits.MaxContentBytes > 0 && options.Limits.MaxRecordBytes >= options.Limits.MaxContentBytes
        && options.Limits.MaxJsonDepth > 0 && options.Limits.MaxMutationBatch > 0 && options.Limits.MaxRecallResults > 0;

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
    private static bool IsLoopback(string? host) => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
}
