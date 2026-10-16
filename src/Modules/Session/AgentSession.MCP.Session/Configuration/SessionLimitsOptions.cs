namespace AgentSession.MCP.Options;

public sealed class SessionLimitsOptions
{
    public const string SectionName = "Memory:Limits";

    public int MaxContentBytes { get; set; } = 65_536;
    public int MaxRecordBytes { get; set; } = 1_048_576;
    public int MaxJsonDepth { get; set; } = 32;
    public int MaxMutationBatch { get; set; } = 100;
    public int MaxRecallResults { get; set; } = 100;
}
