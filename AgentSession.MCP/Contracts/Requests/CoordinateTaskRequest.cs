using AgentSession.MCP.Models;

namespace AgentSession.MCP.Contracts;

public sealed class CoordinateTaskRequest
{
    public required string SessionId { get; set; }
    public required string ActorId { get; set; }
    public required string OperationId { get; set; }
    public required string TaskId { get; set; }
    public required TaskAction Action { get; set; }
    public long ExpectedRevision { get; set; }
    public string? ClaimToken { get; set; }
    public string? WorkKey { get; set; }
    public string? Title { get; set; }
    public string? ParentStepId { get; set; }
    public List<string> Dependencies { get; set; } = [];
    public List<string> AcceptanceExpectations { get; set; } = [];
    public string? NextAction { get; set; }
    public string? Reason { get; set; }
    public List<string> Outputs { get; set; } = [];
    public List<string> Evidence { get; set; } = [];
    public string VerificationStatus { get; set; } = "unverified";
    public string? Handoff { get; set; }
}
