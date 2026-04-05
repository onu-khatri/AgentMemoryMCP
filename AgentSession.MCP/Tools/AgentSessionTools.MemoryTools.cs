using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AgentSession.MCP.Tools;

public sealed partial class AgentSessionTools
{
    [McpServerTool]
    [Description("Reads full persisted session memory plus artifact inventory and recent logs. Use this early when resuming meaningful prior work so current reasoning aligns with persisted context and decisions.")]
    public Task<ReadAgentMemoryResponse> read_agent_memory(ReadAgentMemoryRequest request)
        => WithValidationHandling(() => _service.ReadAgentMemoryAsync(request, CancellationToken.None));

    [McpServerTool]
    [Description("Appends information to the persistent working memory of the active agent session. Use this whenever new knowledge, constraints, assumptions, decisions, or progress updates emerge during reasoning. If losing information would reduce future effectiveness, store it with this tool. This is the primary mechanism for maintaining evolving internal knowledge state. Call it regularly so memory reflects current understanding. sessionId must come from session initialization output. Returns a confirmation on success.")]
    public Task<AppendAgentMemoryResponse> append_agent_memory(AppendAgentMemoryRequest request)
        => WithValidationHandling(() => _service.AppendAgentMemoryAsync(request, CancellationToken.None));

    [McpServerTool]
    [Description("Records significant actions, decisions, and system interactions in the persistent operational log of the active session. Use this whenever behavior changes system state, affects task progress, interacts with external tools, or produces irreversible outcomes. If an external observer needs the information to understand what happened, log it. This is the primary mechanism for behavioral accountability and execution transparency. sessionId must come from session initialization output. Returns a confirmation on success.")]
    public Task<OperationResult> log_agent_event(LogAgentEventRequest request)
        => WithValidationHandling(() => _service.LogAgentEventAsync(request, CancellationToken.None));
}
