using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AgentSession.MCP.Tools;

public sealed partial class AgentSessionTools
{
    [McpServerTool]
    [Description("Saves an agent final implementation plan as a new artifact in the session. Use this before implementation starts to persist the approved plan and handoff details (for example plan version, approved decisions, acceptance criteria, out-of-scope, and validation requirements). A new timestamped file is created on every call and never overwrites prior plans. sessionId must come from session activation output.")]
    public Task<SaveFinalPlanResponse> save_final_plan(SaveFinalPlanRequest request)
        => WithValidationHandling(() => _service.SaveFinalPlanAsync(request, CancellationToken.None));

    [McpServerTool]
    [Description("Fetches the latest saved final implementation plan for a session so agents can resume implementation with the last approved context. Latest is chosen by saved timestamp metadata and includes persisted plan details when available. Returns an explicit not-found response when no final plan exists for the session.")]
    public Task<GetLatestFinalPlanResponse> get_latest_final_plan(GetLatestFinalPlanRequest request)
        => WithValidationHandling(() => _service.GetLatestFinalPlanAsync(request, CancellationToken.None));
}
