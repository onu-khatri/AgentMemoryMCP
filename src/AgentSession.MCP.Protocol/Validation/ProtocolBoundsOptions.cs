namespace AgentSession.MCP.Protocol.Validation;

public sealed class ProtocolBoundsOptions
{
    public int MaxContentBytes { get; init; } = 65_536;
    public int MaxRecordBytes { get; init; } = 1_048_576;
    public int MaxJsonDepth { get; init; } = 32;
    public int MaxMutationBatch { get; init; } = 100;
    public int MaxPageSize { get; init; } = 100;
    public int DefaultPageSize { get; init; } = 50;
    public int MaxResults { get; init; } = 100;
    public int DefaultMaxResults { get; init; } = 10;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}
