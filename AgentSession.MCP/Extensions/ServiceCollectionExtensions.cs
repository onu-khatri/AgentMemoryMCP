using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Options;
using AgentSession.MCP.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSession.MCP.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSessionServer(this IServiceCollection services)
    {
        services.AddOptions<SessionStorageOptions>();

        services.TryAddSingleton<IFileSystem, SystemFileSystem>();
        services.TryAddSingleton<IYamlSerializer, YamlDotNetSerializer>();
        services.TryAddSingleton<SessionStoragePathBuilder>();
        services.TryAddSingleton<IAgentSessionStore, FileAgentSessionStore>();
        services.TryAddSingleton<SessionLifecycleService>();
        services.TryAddSingleton<SessionMemoryService>();
        services.TryAddSingleton<SessionArtifactService>();
        services.TryAddSingleton<FinalPlanService>();
        services.TryAddSingleton<IAgentSessionService, AgentSessionService>();

        return services;
    }
}
