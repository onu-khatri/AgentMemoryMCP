using AgentSession.MCP.AgentMemory.DependencyInjection;
using AgentSession.MCP.Infrastructure.DependencyInjection;
using AgentSession.MCP.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnuObservability.Hosting.Composition;

namespace AgentSession.MCP.Extensions;

internal static class TestAgentMemoryCompositionExtensions
{
    public static IServiceCollection AddMemoryStorageConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAgentSessionInfrastructure(configuration);
        return services.AddAgentMemoryModule(configuration);
    }

    public static OnuObservabilityBuilder AddAgentMemoryTelemetry(
        this OnuObservabilityBuilder builder)
    {
        builder.AddAgentSessionInfrastructureTelemetry();
        return builder.AddOutboundHttpClientInstrumentation(static request =>
            request.RequestUri is { IsLoopback: true } uri
            && uri.AbsolutePath is "/api/tags" or "/api/embed");
    }
}
