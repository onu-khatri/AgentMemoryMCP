using System.ComponentModel;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;
using ModelContextProtocol.Server;

namespace AgentSession.MCP.Tools;

/// <summary>
/// MCP tools for local agent session management and persistence.
/// </summary>
public sealed class AgentSessionTools
{
    private readonly IAgentSessionService _service;

    public AgentSessionTools(IAgentSessionService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Lists all available agent sessions. Use this tool to retrieve existing agent sessions that can be selected to initialize work. Returns a list of session identifiers and metadata for available agent sessions.")]
    public Task<IReadOnlyCollection<ListAgentSessionsItem>> list_agent_sessions()
        => _service.ListAgentSessionsAsync(CancellationToken.None);

    [McpServerTool]
    [Description("Creates or activates the persistent working session for the agent. Use this whenever you start a new conversation, task, or multi-step workflow that requires memory, continuity, artifact storage, or state tracking. Call this before generating plans, performing analysis, writing code, or producing important outputs. This session is your long-term working environment and memory backbone. If sessionId does not exist, a new session is created automatically. Returns active session information and operational instructions.")]
    public async Task<string> create_or_activate_session(CreateOrActivateSessionRequest request)
    {
        try
        {
            return await _service.CreateOrActivateSessionAsync(request, CancellationToken.None);
        }
        catch (ValidationException ex)
        {
            throw new InvalidOperationException($"Invalid request: {ex.Message}", ex);
        }
    }

    [McpServerTool]
    [Description("Reads full persisted session memory plus artifact inventory and recent logs. Use this early when resuming meaningful prior work so current reasoning aligns with persisted context and decisions.")]
    public async Task<ReadAgentMemoryResponse> read_agent_memory(ReadAgentMemoryRequest request)
    {
        try
        {
            return await _service.ReadAgentMemoryAsync(request, CancellationToken.None);
        }
        catch (ValidationException ex)
        {
            throw new InvalidOperationException($"Invalid request: {ex.Message}", ex);
        }
    }

    [McpServerTool]
    [Description("Appends information to the persistent working memory of the active agent session. Use this whenever new knowledge, constraints, assumptions, decisions, or progress updates emerge during reasoning. If losing information would reduce future effectiveness, store it with this tool. This is the primary mechanism for maintaining evolving internal knowledge state. Call it regularly so memory reflects current understanding. sessionId must come from session initialization output. Returns a confirmation on success.")]
    public async Task<AppendAgentMemoryResponse> append_agent_memory(AppendAgentMemoryRequest request)
    {
        try
        {
            return await _service.AppendAgentMemoryAsync(request, CancellationToken.None);
        }
        catch (ValidationException ex)
        {
            throw new InvalidOperationException($"Invalid request: {ex.Message}", ex);
        }
    }

    [McpServerTool]
    [Description("Records significant actions, decisions, and system interactions in the persistent operational log of the active session. Use this whenever behavior changes system state, affects task progress, interacts with external tools, or produces irreversible outcomes. If an external observer needs the information to understand what happened, log it. This is the primary mechanism for behavioral accountability and execution transparency. sessionId must come from session initialization output. Returns a confirmation on success.")]
    public async Task<OperationResult> log_agent_event(LogAgentEventRequest request)
    {
        try
        {
            return await _service.LogAgentEventAsync(request, CancellationToken.None);
        }
        catch (ValidationException ex)
        {
            throw new InvalidOperationException($"Invalid request: {ex.Message}", ex);
        }
    }

    [McpServerTool]
    [Description("Creates and persists a structured artifact within the current agent session. Use this whenever you produce knowledge, decisions, analysis, plans, mappings, summaries, extracted data, or other information that can improve future reasoning, avoid recomputation, or support continuity. If losing information would reduce effectiveness later, store it here. This acts as long-term working memory for the session. Do not use it for transient thoughts, exploratory reasoning, or disposable intermediate steps. sessionId must come from session initialization output. Returns a confirmation on success.")]
    public async Task<CreateAgentArtifactResponse> create_agent_artifact(CreateAgentArtifactRequest request)
    {
        try
        {
            return await _service.CreateAgentArtifactAsync(request, CancellationToken.None);
        }
        catch (ValidationException ex)
        {
            throw new InvalidOperationException($"Invalid request: {ex.Message}", ex);
        }
    }

    [McpServerTool]
    [Description("Retrieves the content of a specific artifact stored in the current agent session. Use this whenever you need previously stored knowledge, decisions, analysis, plans, mappings, summaries, extracted data, or other persisted information created via create_agent_artifact. This lets you leverage long-term working memory for better reasoning and decision-making. artifactName must match an existing artifact in the session. Returns artifact content and metadata, or an explicit not-found error.")]
    public async Task<ReadAgentArtifactResponse> read_agent_artifact(ReadAgentArtifactRequest request)
    {
        try
        {
            return await _service.ReadAgentArtifactAsync(request, CancellationToken.None);
        }
        catch (ValidationException ex)
        {
            throw new InvalidOperationException($"Invalid request: {ex.Message}", ex);
        }
    }

    [McpServerTool]
    [Description("Lists all artifacts stored in the current agent session with metadata. Use this for an overview of persisted knowledge, decisions, analysis, plans, mappings, summaries, extracted data, and other saved outputs. This helps identify what long-term information is available and which artifacts should be read or referenced for current reasoning and decisions. sessionId must come from session activation output. Returns metadata for all artifacts in the session.")]
    public async Task<ListAgentArtifactsResponse> list_agent_artifacts(ListAgentArtifactsRequest request)
    {
        try
        {
            return await _service.ListAgentArtifactsAsync(request, CancellationToken.None);
        }
        catch (ValidationException ex)
        {
            throw new InvalidOperationException($"Invalid request: {ex.Message}", ex);
        }
    }
}
