namespace AgentSession.MCP.Models;

public sealed class SessionWorkingContext
{
    public string? Objective { get; set; }
    public List<string>? PlanReferences { get; set; }
    public List<string>? Constraints { get; set; }
    public List<string>? NextActions { get; set; }
}
