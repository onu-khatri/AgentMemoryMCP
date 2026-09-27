namespace AgentSession.MCP.Models.Memory;

public sealed class MemoryEvent
{
    public int SchemaVersion { get; set; } = 1;
    public required string EventId { get; set; }
    public string? MemoryId { get; set; }
    public required string RepositoryId { get; set; }
    public required DateTimeOffset TimestampUtc { get; set; }
    public string? AgentId { get; set; }
    public string? SessionId { get; set; }
    public string? TaskId { get; set; }
    public required string EventType { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}
