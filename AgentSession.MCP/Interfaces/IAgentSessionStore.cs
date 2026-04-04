using AgentSession.MCP.Models;

namespace AgentSession.MCP.Interfaces;

public interface IAgentSessionStore
{
    Task<IReadOnlyCollection<AgentSessionSummary>> ListSessionsAsync(CancellationToken cancellationToken);

    Task<(AgentSessionState Session, bool Created, AgentSessionPaths Paths)> CreateOrActivateSessionAsync(
        string? requestedSessionId,
        string? initialState,
        string? agentName,
        CancellationToken cancellationToken);

    Task<string> ReadMemoryAsync(string sessionId, CancellationToken cancellationToken);

    Task<long> AppendMemoryAsync(string sessionId, string content, string? agentName, string? sectionTitle, CancellationToken cancellationToken);

    Task AppendLogAsync(string sessionId, AgentSessionLogEntry entry, CancellationToken cancellationToken);

    Task<AgentArtifactDocument> CreateOrUpdateArtifactAsync(
        string sessionId,
        string artifactName,
        string description,
        string intendedUse,
        string content,
        string? contentType,
        IReadOnlyCollection<string>? tags,
        bool overwrite,
        CancellationToken cancellationToken);

    Task<AgentArtifactDocument> ReadArtifactAsync(string sessionId, string artifactName, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AgentArtifactMetadata>> ListArtifactsAsync(string sessionId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AgentSessionLogEntry>> ReadLogsAsync(string sessionId, int takeLast, CancellationToken cancellationToken);

    Task<AgentSessionState> GetSessionStateAsync(string sessionId, CancellationToken cancellationToken);
}
