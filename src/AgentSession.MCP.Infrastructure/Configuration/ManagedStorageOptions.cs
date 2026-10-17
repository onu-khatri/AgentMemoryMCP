namespace AgentSession.MCP.Infrastructure.Configuration;

public sealed class ManagedStorageOptions
{
    public const string SectionName = "Memory:Limits";

    public int MaxRecordBytes { get; set; } = 1_048_576;
    public int MaxMutationBatch { get; set; } = 100;
    public int MaxRecallResults { get; set; } = 100;
    public int MaxArchiveCompressedBytes { get; set; } = 16_777_216;
}
