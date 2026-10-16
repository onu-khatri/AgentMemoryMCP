using AgentSession.MCP.Models;

namespace AgentSession.MCP.Contracts;

public sealed class UpdateSessionArtifactsRequest
{
    public required string SessionId { get; set; }
    public required string ActorId { get; set; }
    public required string OperationId { get; set; }
    public required List<SessionArtifactUpdate> Updates { get; set; }
}

public sealed record SessionArtifactUpdate(
    SharedSessionArtifact Artifact,
    long ExpectedRevision,
    string? ClaimToken = null
);
