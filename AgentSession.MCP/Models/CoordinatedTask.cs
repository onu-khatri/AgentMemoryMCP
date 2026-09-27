namespace AgentSession.MCP.Models;

public sealed class CoordinatedTask
{
    public required string Id { get; set; }
    public required string WorkKey { get; set; }
    public required string Title { get; set; }
    public string? ParentStepId { get; set; }
    public string DefinitionHash { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = [];
    public List<string> AcceptanceExpectations { get; set; } = [];
    public string? NextAction { get; set; }
    public WorkStatus Status { get; set; }
    public string? OwnerId { get; set; }
    public string? ClaimToken { get; set; }
    public long FencingGeneration { get; set; }
    public DateTimeOffset? ClaimExpiresAtUtc { get; set; }
    public long Revision { get; set; }
    public string? Blocker { get; set; }
    public List<string> Outputs { get; set; } = [];
    public List<string> Evidence { get; set; } = [];
    public string VerificationStatus { get; set; } = "unverified";
    public string? Handoff { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
