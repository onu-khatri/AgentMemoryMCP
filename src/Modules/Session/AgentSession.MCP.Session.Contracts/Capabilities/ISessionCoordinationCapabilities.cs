using AgentSession.MCP.Contracts;
using AgentSession.MCP.Core.Context;

namespace AgentSession.MCP.Session.Contracts.Capabilities;

public interface ISessionCoordinationCapabilities
{
    Task<SessionMutationResult> UpdateArtifactsAsync(
        UpdateSessionArtifactsRequest request,
        PlatformRequestContext context,
        CancellationToken cancellationToken);

    Task<SessionMutationResult> CoordinateAsync(
        CoordinateTaskRequest request,
        PlatformRequestContext context,
        CancellationToken cancellationToken);
}
