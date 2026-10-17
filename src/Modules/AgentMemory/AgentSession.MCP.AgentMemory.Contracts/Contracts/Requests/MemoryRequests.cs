using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Contracts;

public enum RememberTier
{
    Temp,
    Short,
    LongCandidate,
}

public sealed record GetMemoryRequest(
    string MemoryId,
    bool IncludeArchived = false,
    bool IncludeHistory = false
);

public sealed record UpdateMemoryRequest(
    string MemoryId,
    string OperationId,
    long ExpectedRevision,
    string AgentId,
    [property: MaxLength(65_536)] string Content,
    JsonElement? StructuredData = null,
    string? Title = null,
    List<string>? Tags = null,
    DateTimeOffset? ExpiresAtUtc = null
);

public sealed record ArchiveMemoryItem(string MemoryId, long ExpectedRevision);

public sealed record ArchiveMemoryRequest(
    string OperationId,
    string AgentId,
    string Reason,
    [property: MinLength(1), MaxLength(100)] List<ArchiveMemoryItem> Records
);

public sealed record PrepareCompactionRequest(
    string OperationId,
    string AgentId,
    [property: MinLength(2), MaxLength(100)] List<ArchiveMemoryItem> Sources
);

public sealed record PreparedCompactionResult(
    string PreparationToken,
    IReadOnlyList<MemoryRecord> Sources
);

public sealed record CompactionCoverageInput(
    string MemoryId,
    List<string> Evidence,
    List<string> Contradictions
);

public sealed record CommitCompactionRequest(
    string OperationId,
    string PreparationToken,
    string AgentId,
    string Reason,
    string Title,
    string Summary,
    List<string> Facts,
    List<string> OpenQuestions,
    List<string> Contradictions,
    List<string> SuccessfulPatterns,
    List<string> FailedPatterns,
    List<string> Evidence,
    [property: MinLength(2), MaxLength(100)] List<ArchiveMemoryItem> Sources,
    [property: MinLength(2), MaxLength(100)] List<CompactionCoverageInput> SourceCoverage,
    [property: MaxLength(100)] List<string> Tags,
    string? DecisionArea = null,
    string? SessionId = null,
    string? TaskId = null,
    bool ArchiveOriginals = true,
    CompactionKind Kind = CompactionKind.Semantic
);

public sealed record CompactMemoryRequest(
    string OperationId,
    string AgentId,
    string Reason,
    string Title,
    string Summary,
    List<string> Facts,
    List<string> OpenQuestions,
    List<string> Contradictions,
    List<string> SuccessfulPatterns,
    List<string> FailedPatterns,
    List<string> Evidence,
    [property: MinLength(2), MaxLength(100)] List<ArchiveMemoryItem> Sources,
    [property: MinLength(2), MaxLength(100)] List<CompactionCoverageInput> SourceCoverage,
    [property: MaxLength(100)] List<string> Tags,
    string? DecisionArea = null,
    string? SessionId = null,
    string? TaskId = null,
    bool ArchiveOriginals = true,
    CompactionKind Kind = CompactionKind.Semantic
);

public sealed record CompactionResult(
    string MemoryId,
    long Revision,
    IReadOnlyList<string> SourceMemoryIds,
    string ArchiveId,
    IReadOnlyList<string> ArchivedMemoryIds,
    IReadOnlyList<string> Contradictions,
    CompactionKind Kind
);

public sealed record RecordOutcomeRequest(
    string MemoryId,
    string OutcomeId,
    long ExpectedRevision,
    string SessionId,
    string TaskId,
    string AgentId,
    OutcomeKind Result,
    [property: MinLength(1), MaxLength(100)] List<string> Evidence,
    string? Notes = null
);

public sealed record LongTermLifecycleRequest(
    string MemoryId,
    string OperationId,
    long ExpectedRevision,
    string AgentId,
    string Reason,
    [property: MinLength(1), MaxLength(100)] List<string> Evidence,
    string? ReplacementMemoryId = null
);

public sealed record DeleteMemoryRequest(
    string MemoryId,
    string OperationId,
    long ExpectedRevision,
    string AgentId,
    string Reason
);

public sealed record PromoteMemoryRequest(
    string MemoryId,
    string OperationId,
    long ExpectedRevision,
    string AgentId,
    string Reason,
    bool AuthoritativeVerification = false,
    List<string>? VerificationEvidence = null
);

public sealed record ReviewMemoryRequest(
    string MemoryId,
    string OperationId,
    long ExpectedRevision,
    string AgentId,
    ReviewState Action,
    string Reason,
    [property: MinLength(1), MaxLength(100)] List<string> Evidence
);

public sealed record ReviewListRequest(
    [property: Range(1, 100)] int MaxResults = 10,
    DateTimeOffset? OlderThanUtc = null,
    ReviewState? ReviewState = null,
    string? Category = null,
    string? DecisionArea = null,
    string? AgentId = null,
    string? TaskId = null
);

public sealed record RecalledMemory(
    MemoryRecord Record,
    string MatchType,
    double? Score,
    bool Advisory = true
);
