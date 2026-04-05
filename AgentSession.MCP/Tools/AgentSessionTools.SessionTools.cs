using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AgentSession.MCP.Tools;

public sealed partial class AgentSessionTools
{
    [McpServerTool]
    [Description("Lists all available agent sessions. Use this tool to retrieve existing agent sessions that can be selected to initialize work. Returns a list of session identifiers and metadata for available agent sessions.")]
    public Task<IReadOnlyCollection<ListAgentSessionsItem>> list_agent_sessions()
        => _service.ListAgentSessionsAsync(CancellationToken.None);

    [McpServerTool]
    [Description("Creates or activates the persistent working session for the agent. Use this whenever you start a new conversation, task, or multi-step workflow that requires memory, continuity, artifact storage, or state tracking. Call this before generating plans, performing analysis, writing code, or producing important outputs. This session is your long-term working environment and memory backbone. If sessionId does not exist, a new session is created automatically. Returns active session information and operational instructions.")]
    public Task<string> create_or_activate_session(CreateOrActivateSessionRequest request)
        => WithValidationHandling(() => _service.CreateOrActivateSessionAsync(request, CancellationToken.None));
}
