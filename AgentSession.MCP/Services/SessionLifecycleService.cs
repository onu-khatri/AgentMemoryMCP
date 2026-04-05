using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Models;
using AgentSession.MCP.Tools;

namespace AgentSession.MCP.Services;

public sealed class SessionLifecycleService
{
    private readonly IAgentSessionStore _store;
    private readonly IYamlSerializer _yamlSerializer;

    public SessionLifecycleService(IAgentSessionStore store, IYamlSerializer yamlSerializer)
    {
        _store = store;
        _yamlSerializer = yamlSerializer;
    }

    public async Task<IReadOnlyCollection<ListAgentSessionsItem>> ListSessionsAsync(CancellationToken cancellationToken)
    {
        var sessions = await _store.ListSessionsAsync(cancellationToken);
        return sessions
            .Select(item => new ListAgentSessionsItem
            {
                SessionId = item.SessionId,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                CurrentStateSummary = item.CurrentStateSummary,
                ArtifactCount = item.ArtifactCount,
                LastLogTimestamp = item.LastLogTimestamp,
                Description = item.Description
            })
            .ToList();
    }

    public async Task<string> CreateOrActivateSessionAsync(CreateOrActivateSessionRequest request, CancellationToken cancellationToken)
    {
        var (session, created, paths) = await _store.CreateOrActivateSessionAsync(
            request.SessionId,
            request.InitialState,
            request.AgentName,
            cancellationToken);

        var payload = new
        {
            session_id = session.SessionId,
            session_path = paths.SessionPath,
            created_or_activated = created ? "created" : "activated",
            current_state = session.CurrentState,
            usage_instruction = session.UsageInstruction,
            available_files = new[]
            {
                AgentSessionState.AgentSessionStateFileName,
                AgentSessionState.AgentMemoryFileName,
                AgentSessionState.AgentSessionLogFileName,
                "artifacts/"
            },
            artifact_folder_path = paths.ArtifactDirectoryPath
        };

        return _yamlSerializer.Serialize(payload);
    }
}
