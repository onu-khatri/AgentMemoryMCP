using AgentSession.MCP.Contracts;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Models;

namespace AgentSession.MCP.Session.Contracts.Capabilities;

public interface ISessionResumeCapabilities
{
    Task<SessionResumePage> ResumeAsync(
        ResumeSessionRequest request,
        PlatformRequestContext context,
        CancellationToken cancellationToken);

    Task<AgentCheckpoint> CheckpointAsync(
        CheckpointSessionRequest request,
        PlatformRequestContext context,
        CancellationToken cancellationToken);
}
