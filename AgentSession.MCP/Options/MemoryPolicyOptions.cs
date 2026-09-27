namespace AgentSession.MCP.Options;

public sealed class MemoryPolicyOptions
{
    public TempMemoryOptions Temp { get; set; } = new();
    public ShortMemoryOptions Short { get; set; } = new();
    public LongMemoryOptions Long { get; set; } = new();
    public MemoryLimits Limits { get; set; } = new();
}

public sealed class TempMemoryOptions
{
    public double MaxAgeHours { get; set; } = 24;
    public double CleanupIntervalMinutes { get; set; } = 60;
    public int MaxEntriesPerSession { get; set; } = 1_000;
}

public sealed class ShortMemoryOptions
{
    public double ReviewAfterDays { get; set; } = 3;
    public double CompactionCandidateAfterDays { get; set; } = 7;
    public double ArchiveAfterDays { get; set; } = 14;
    public int MaxActiveRecords { get; set; } = 10_000;
    public string ArchiveCompression { get; set; } = "gzip";
}

public sealed class LongMemoryOptions
{
    public int RequiredIndependentConfirmations { get; set; } = 2;
    // Null means no additional score cutoff; this is not a claim-confidence threshold.
    public double? MinimumSimilarity { get; set; }
    public int DefaultMaxResults { get; set; } = 10;
    public bool AllowAuthoritativeVerification { get; set; }
}

public sealed class MemoryLimits
{
    public int MaxContentBytes { get; set; } = 65_536;
    public int MaxRecordBytes { get; set; } = 1_048_576;
    public int MaxJsonDepth { get; set; } = 32;
    public int MaxMutationBatch { get; set; } = 100;
    public int MaxRecallResults { get; set; } = 100;
    public int MaxEventJournalBytes { get; set; } = 262_144;
    public int MaxArchiveCompressedBytes { get; set; } = 16_777_216;
    public int MaxArchiveExpandedBytes { get; set; } = 67_108_864;
}

public sealed class EmbeddingOptions
{
    public string Provider { get; set; } = "ollama";
    public double TimeoutSeconds { get; set; } = 30;
    public int MaxBatchSize { get; set; } = 32;
    public int MaxInputBytes { get; set; } = 262_144;
    public OllamaOptions Ollama { get; set; } = new();
}

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "embeddinggemma";
}

public sealed class QdrantOptions
{
    public string Host { get; set; } = "localhost";
    public int GrpcPort { get; set; } = 6334;
    public int RestPort { get; set; } = 6333;
    public string Collection { get; set; } = "repo_ai_learning_v1";
}
