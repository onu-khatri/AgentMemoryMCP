using AgentSession.MCP.Contracts;
using AgentSession.MCP.Core.Context;

namespace AgentSession.MCP.Session.Contracts.Capabilities;

public interface ISessionLifecycleCapabilities
{
    Task<SharedSessionActivation> ActivateAsync(
        ActivateSharedSessionRequest request,
        PlatformRequestContext context,
        CancellationToken cancellationToken);

    Task<SharedSessionList> ListAsync(
        ListSharedSessionsRequest request,
        PlatformRequestContext context,
        CancellationToken cancellationToken);
}
