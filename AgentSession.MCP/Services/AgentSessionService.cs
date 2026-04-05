using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Tools;

namespace AgentSession.MCP.Services;

public sealed class AgentSessionService : IAgentSessionService
{
    private readonly SessionLifecycleService _lifecycle;
    private readonly SessionMemoryService _memory;
    private readonly SessionArtifactService _artifact;
    private readonly FinalPlanService _finalPlan;

    public AgentSessionService(
        SessionLifecycleService lifecycle,
        SessionMemoryService memory,
        SessionArtifactService artifact,
        FinalPlanService finalPlan)
    {
        _lifecycle = lifecycle;
        _memory = memory;
        _artifact = artifact;
        _finalPlan = finalPlan;
    }

    public async Task<IReadOnlyCollection<ListAgentSessionsItem>> ListAgentSessionsAsync(CancellationToken cancellationToken)
        => await _lifecycle.ListSessionsAsync(cancellationToken);

    public async Task<string> CreateOrActivateSessionAsync(CreateOrActivateSessionRequest request, CancellationToken cancellationToken)
        => await _lifecycle.CreateOrActivateSessionAsync(request, cancellationToken);

    public async Task<ReadAgentMemoryResponse> ReadAgentMemoryAsync(ReadAgentMemoryRequest request, CancellationToken cancellationToken)
        => await _memory.ReadAgentMemoryAsync(request, cancellationToken);

    public async Task<AppendAgentMemoryResponse> AppendAgentMemoryAsync(AppendAgentMemoryRequest request, CancellationToken cancellationToken)
        => await _memory.AppendAgentMemoryAsync(request, cancellationToken);

    public async Task<OperationResult> LogAgentEventAsync(LogAgentEventRequest request, CancellationToken cancellationToken)
        => await _memory.LogAgentEventAsync(request, cancellationToken);

    public async Task<CreateAgentArtifactResponse> CreateAgentArtifactAsync(CreateAgentArtifactRequest request, CancellationToken cancellationToken)
        => await _artifact.CreateAgentArtifactAsync(request, cancellationToken);

    public async Task<ReadAgentArtifactResponse> ReadAgentArtifactAsync(ReadAgentArtifactRequest request, CancellationToken cancellationToken)
        => await _artifact.ReadAgentArtifactAsync(request, cancellationToken);

    public async Task<ListAgentArtifactsResponse> ListAgentArtifactsAsync(ListAgentArtifactsRequest request, CancellationToken cancellationToken)
        => await _artifact.ListAgentArtifactsAsync(request, cancellationToken);

    public async Task<SaveFinalPlanResponse> SaveFinalPlanAsync(SaveFinalPlanRequest request, CancellationToken cancellationToken)
        => await _finalPlan.SaveFinalPlanAsync(request, cancellationToken);

    public async Task<GetLatestFinalPlanResponse> GetLatestFinalPlanAsync(GetLatestFinalPlanRequest request, CancellationToken cancellationToken)
        => await _finalPlan.GetLatestFinalPlanAsync(request, cancellationToken);
}
