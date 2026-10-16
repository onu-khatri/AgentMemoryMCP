using AgentSession.MCP.Models;

namespace AgentSession.MCP.Contracts;

public sealed record SessionMutationResult(
    long Sequence,
    Dictionary<string, long> ArtifactRevisions,
    CoordinatedTask? Task = null
);
