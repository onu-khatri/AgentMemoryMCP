using AgentSession.MCP.Contracts;
using AgentSession.MCP.Core.Context;

namespace AgentSession.MCP.AgentMemory.Contracts.Capabilities;

public interface IMemoryQueryCapabilities
{
    Task<MemoryStatusResult> GetStatusAsync(
        PlatformRequestContext context,
        CancellationToken cancellationToken);

    Task<MemoryReadResult> GetAsync(
        GetMemoryRequest request,
        PlatformRequestContext context,
        CancellationToken cancellationToken);

    Task<RecallMemoryResult> RecallAsync(
        RecallMemoryRequest request,
        PlatformRequestContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReviewMemoryCandidate>> ListReviewAsync(
        ReviewListRequest request,
        PlatformRequestContext context,
        CancellationToken cancellationToken);
}
