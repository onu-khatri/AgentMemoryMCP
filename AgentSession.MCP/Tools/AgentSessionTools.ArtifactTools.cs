using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AgentSession.MCP.Tools;

public sealed partial class AgentSessionTools
{
    [McpServerTool]
    [Description("Creates and persists a structured artifact within the current agent session. Use this whenever you produce knowledge, decisions, analysis, plans, mappings, summaries, extracted data, or other information that can improve future reasoning, avoid recomputation, or support continuity. If losing information would reduce effectiveness later, store it here. This acts as long-term working memory for the session. Do not use it for transient thoughts, exploratory reasoning, or disposable intermediate steps. sessionId must come from session initialization output. Returns a confirmation on success.")]
    public Task<CreateAgentArtifactResponse> create_agent_artifact(CreateAgentArtifactRequest request)
        => WithValidationHandling(() => _service.CreateAgentArtifactAsync(request, CancellationToken.None));

    [McpServerTool]
    [Description("Retrieves the content of a specific artifact stored in the current agent session. Use this whenever you need previously stored knowledge, decisions, analysis, plans, mappings, summaries, extracted data, or other persisted information created via create_agent_artifact. This lets you leverage long-term working memory for better reasoning and decision-making. artifactName must match an existing artifact in the session. Returns artifact content and metadata, or an explicit not-found error.")]
    public Task<ReadAgentArtifactResponse> read_agent_artifact(ReadAgentArtifactRequest request)
        => WithValidationHandling(() => _service.ReadAgentArtifactAsync(request, CancellationToken.None));

    [McpServerTool]
    [Description("Lists all artifacts stored in the current agent session with metadata. Use this for an overview of persisted knowledge, decisions, analysis, plans, mappings, summaries, extracted data, and other saved outputs. This helps identify what long-term information is available and which artifacts should be read or referenced for current reasoning and decisions. sessionId must come from session activation output. Returns metadata for all artifacts in the session.")]
    public Task<ListAgentArtifactsResponse> list_agent_artifacts(ListAgentArtifactsRequest request)
        => WithValidationHandling(() => _service.ListAgentArtifactsAsync(request, CancellationToken.None));
}
