using AgentSession.MCP.Contracts;
using AgentSession.MCP.Core.Context;

namespace AgentSession.MCP.AgentMemory.Contracts.Capabilities;

public interface IMemoryIndexCapabilities
{
    Task<ReindexMemoryResult> ReindexAsync(
        ReindexMemoryRequest request,
        PlatformRequestContext context,
        CancellationToken cancellationToken);

    Task<int> RebuildIndexesAsync(
        PlatformRequestContext context,
        CancellationToken cancellationToken);
}
