namespace AgentSession.MCP.Options;

public sealed class SessionCoordinationOptions
{
    public double ClaimLeaseMinutes { get; set; } = 15;
    public double SnapshotLifetimeMinutes { get; set; } = 60;
}
