using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Tools;

namespace AgentSession.MCP.Services;

public sealed class FinalPlanService
{
    private readonly IAgentSessionStore _store;

    public FinalPlanService(IAgentSessionStore store)
    {
        _store = store;
    }

    public async Task<SaveFinalPlanResponse> SaveFinalPlanAsync(SaveFinalPlanRequest request, CancellationToken cancellationToken)
    {
        var sessionId = AgentSessionValidation.ValidateSessionId(request.SessionId);
        AgentSessionValidation.RequireValue(request.PlanContent, "planContent");

        var plan = await _store.SaveFinalPlanAsync(
            sessionId,
            request.PlanContent,
            request.PlanTitle,
            request.AgentName,
            AgentSessionContractMapper.ToFinalPlanDetails(request),
            cancellationToken);

        return new SaveFinalPlanResponse
        {
            Success = true,
            SessionId = sessionId,
            ArtifactName = plan.Metadata.Name,
            FileName = plan.Metadata.FileName,
            SavedAt = plan.Metadata.UpdatedAt
        };
    }

    public async Task<GetLatestFinalPlanResponse> GetLatestFinalPlanAsync(GetLatestFinalPlanRequest request, CancellationToken cancellationToken)
    {
        var sessionId = AgentSessionValidation.ValidateSessionId(request.SessionId);
        var latest = await _store.GetLatestFinalPlanAsync(sessionId, cancellationToken);
        if (latest is null)
        {
            return new GetLatestFinalPlanResponse
            {
                Success = false,
                NotFound = true,
                Message = "No final plan exists for this session.",
                SessionId = sessionId,
                Metadata = null,
                PlanContent = null
            };
        }

        return new GetLatestFinalPlanResponse
        {
            Success = true,
            NotFound = false,
            Message = "Latest final plan retrieved successfully.",
            SessionId = sessionId,
            Metadata = AgentSessionContractMapper.ToArtifactItem(latest.Metadata),
            PlanContent = latest.Content,
            PlanDetails = AgentSessionContractMapper.ToFinalPlanDetailsItem(latest.Metadata.FinalPlanDetails)
        };
    }
}
