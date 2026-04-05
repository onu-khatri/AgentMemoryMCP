using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AgentSession.MCP.Tools;

public sealed partial class AgentSessionTools
{
    [McpServerTool]
    [Description("Saves an agent final implementation plan as a new artifact in the session. Use this to persist finalized implementation plans for future continuity. A new timestamped file is created on every call and never overwrites prior plans. sessionId must come from session activation output.")]
    public Task<SaveFinalPlanResponse> save_final_plan(SaveFinalPlanRequest request)
        => WithValidationHandling(() => _service.SaveFinalPlanAsync(request, CancellationToken.None));

    [McpServerTool]
    [Description("Fetches the latest saved final implementation plan for a session. Latest is chosen by saved timestamp metadata. Returns an explicit not-found response when no final plan exists for the session.")]
    public Task<GetLatestFinalPlanResponse> get_latest_final_plan(GetLatestFinalPlanRequest request)
        => WithValidationHandling(() => _service.GetLatestFinalPlanAsync(request, CancellationToken.None));
}
