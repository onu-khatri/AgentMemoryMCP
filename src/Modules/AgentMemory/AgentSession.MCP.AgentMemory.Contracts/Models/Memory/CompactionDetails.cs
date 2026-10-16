namespace AgentSession.MCP.Models.Memory;

public enum CompactionKind
{
    Semantic,
    ReviewedDuplicateMerge,
}

public sealed class CompactionSourceCoverage
{
    public required string MemoryId { get; set; }
    public List<string> Evidence { get; set; } = [];
    public List<string> Contradictions { get; set; } = [];
}

public sealed class CompactionDetails
{
    public CompactionKind Kind { get; set; }
    public required List<string> SourceMemoryIds { get; set; }
    public required string Summary { get; set; }
    public List<string> Facts { get; set; } = [];
    public List<string> OpenQuestions { get; set; } = [];
    public List<string> Contradictions { get; set; } = [];
    public List<string> SuccessfulPatterns { get; set; } = [];
    public List<string> FailedPatterns { get; set; } = [];
    public required DateTimeOffset CompactedAtUtc { get; set; }
    public required string AgentId { get; set; }
    public required string Reason { get; set; }
    public List<string> Evidence { get; set; } = [];
    public List<CompactionSourceCoverage> SourceCoverage { get; set; } = [];
}
