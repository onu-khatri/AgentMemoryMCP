namespace AgentSession.MCP.Options;

public sealed class SessionCoordinationOptions
{
    public const string SectionName = "SessionCoordination";

    public double ClaimLeaseMinutes { get; set; } = 15;
    public double SnapshotLifetimeMinutes { get; set; } = 60;
}
