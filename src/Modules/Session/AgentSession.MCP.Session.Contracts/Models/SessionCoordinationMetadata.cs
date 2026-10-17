namespace AgentSession.MCP.Models;

public sealed class SessionCoordinationMetadata
{
    public int SchemaVersion { get; set; } = 1;
    public required string RepositoryId { get; set; }
    public required string SessionId { get; set; }
    public long Sequence { get; set; }
    public Dictionary<string, long> ArtifactRevisions { get; set; } = [];
    public Dictionary<string, CoordinatedTask> Tasks { get; set; } = [];
    public Dictionary<string, AgentCheckpoint> Checkpoints { get; set; } = [];
    public List<SessionChange> Changes { get; set; } = [];
}
