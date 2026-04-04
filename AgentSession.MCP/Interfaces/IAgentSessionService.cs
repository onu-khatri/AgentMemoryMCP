using AgentSession.MCP.Tools;

namespace AgentSession.MCP.Interfaces;

public interface IAgentSessionService
{
    Task<IReadOnlyCollection<ListAgentSessionsItem>> ListAgentSessionsAsync(CancellationToken cancellationToken);

    Task<string> CreateOrActivateSessionAsync(CreateOrActivateSessionRequest request, CancellationToken cancellationToken);

    Task<ReadAgentMemoryResponse> ReadAgentMemoryAsync(ReadAgentMemoryRequest request, CancellationToken cancellationToken);

    Task<AppendAgentMemoryResponse> AppendAgentMemoryAsync(AppendAgentMemoryRequest request, CancellationToken cancellationToken);

    Task<OperationResult> LogAgentEventAsync(LogAgentEventRequest request, CancellationToken cancellationToken);

    Task<CreateAgentArtifactResponse> CreateAgentArtifactAsync(CreateAgentArtifactRequest request, CancellationToken cancellationToken);

    Task<ReadAgentArtifactResponse> ReadAgentArtifactAsync(ReadAgentArtifactRequest request, CancellationToken cancellationToken);

    Task<ListAgentArtifactsResponse> ListAgentArtifactsAsync(ListAgentArtifactsRequest request, CancellationToken cancellationToken);
}
