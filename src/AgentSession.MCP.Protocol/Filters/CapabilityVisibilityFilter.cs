using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Core.Errors;
using AgentSession.MCP.Core.Execution;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentSession.MCP.Protocol.Filters;

public static class CapabilityVisibilityFilter
{
    public static ListToolsResult FilterVisible(ListToolsResult result, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(services);

        var accessor = services.GetRequiredService<IPlatformRequestContextAccessor>();
        PlatformRequestContext context;
        try
        {
            context = accessor.Current;
        }
        catch (PlatformException)
        {
            return new ListToolsResult { Tools = [] };
        }

        result.Tools = result.Tools
            .Where(tool => CapabilityCatalog.TryGet(tool.Name, out var descriptor)
                && CapabilityAuthorizationFilter.IsVisible(context, descriptor))
            .ToList();
        return result;
    }
}
