namespace AgentSession.MCP.Models;

public sealed class AgentSessionSummary
{
    public string SessionId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? CurrentStateSummary { get; set; }

    public int ArtifactCount { get; set; }

    public DateTimeOffset? LastLogTimestamp { get; set; }

    public string? Description { get; set; }
}
