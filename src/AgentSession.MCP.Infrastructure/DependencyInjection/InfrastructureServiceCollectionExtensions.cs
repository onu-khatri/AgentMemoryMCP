using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Core.Observability;
using AgentSession.MCP.Core.Storage;
using AgentSession.MCP.Infrastructure.Configuration;
using AgentSession.MCP.Infrastructure.Observability;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OnuObservability.Mcp.Health;

namespace AgentSession.MCP.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSessionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<SystemStorageOptions>()
            .Bind(configuration.GetSection(SystemStorageOptions.SectionName))
            .Validate(StorageOptionsValidator.ValidRoot, StorageOptionsValidator.RootMessage)
            .ValidateOnStart();
        services
            .AddOptions<RepositoryOptions>()
            .Bind(configuration.GetSection(RepositoryOptions.SectionName))
            .Validate(
                StorageOptionsValidator.ValidRepository,
                StorageOptionsValidator.RepositoryMessage
            )
            .ValidateOnStart();
        services
            .AddOptions<ManagedStorageOptions>()
            .Bind(configuration.GetSection(ManagedStorageOptions.SectionName))
            .Validate(StorageOptionsValidator.ValidLimits, StorageOptionsValidator.LimitsMessage)
            .ValidateOnStart();

        services.TryAddSingleton<ManagedStoragePaths>();
        services.TryAddSingleton<IManagedStorageContext>(provider =>
            provider.GetRequiredService<ManagedStoragePaths>()
        );
        services.TryAddSingleton(provider =>
            new RepositoryResource(provider.GetRequiredService<IOptions<RepositoryOptions>>().Value.Id)
        );
        services.TryAddSingleton<ManagedStoragePathResolver>();
        services.TryAddSingleton<IManagedStoragePathResolver>(provider =>
            provider.GetRequiredService<ManagedStoragePathResolver>()
        );
        services.TryAddSingleton<SystemFileSystem>();
        services.TryAddSingleton<IMcpDependencyFailureRecorder, NoOpDependencyFailureRecorder>();
        services.TryAddSingleton<IDependencyOperationTracker, DependencyOperationTracker>();
        services.TryAddSingleton<RepositoryMutationLock>();
        services.TryAddSingleton<ManagedTransactionStore>();
        services.TryAddSingleton<IManagedTransactionStore>(provider =>
            provider.GetRequiredService<ManagedTransactionStore>()
        );
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
