using System.Text.Json;

namespace AgentSession.MCP.Models.Memory;

public enum MemoryTier
{
    Temp,
    Short,
    Long,
}

public enum ReviewState
{
    Unreviewed,
    Keep,
    CompactCandidate,
    PromotionCandidate,
    ArchiveCandidate,
    DeleteCandidate,
    Reviewed,
}

public enum MemoryState
{
    Active,
    Compacted,
    Archived,
    Deleted,
    Promoted,
    Candidate,
    Validated,
    Superseded,
    Retired,
    Rejected,
}

public enum EmbeddingState
{
    NotRequired,
    PendingEmbedding,
    Ready,
    Failed,
}

public enum VectorIndexState
{
    NotRequired,
    Pending,
    Indexed,
    Failed,
}

public enum OutcomeKind
{
    Success,
    PartialSuccess,
    Failure,
    Contradicted,
    NotApplicable,
    Stale,
}

public enum RelationshipKind
{
    DuplicateOf,
    RelatedTo,
    DerivedFrom,
    Supersedes,
    SupersededBy,
    CompactedFrom,
    PromotedFrom,
}

public sealed record MemoryRelationship(RelationshipKind Kind, string MemoryId);

public sealed class MemoryRecord
{
    public int SchemaVersion { get; set; } = 1;
    public required string Id { get; set; }
    public required string RepositoryId { get; set; }
    public required MemoryTier Tier { get; set; }
    public string? SessionId { get; set; }
    public string? TaskId { get; set; }
    public string? ParentStepId { get; set; }
    public string? AgentId { get; set; }
    public string Category { get; set; } = "observation";
    public string? DecisionArea { get; set; }
    public string Title { get; set; } = string.Empty;
    public required string Content { get; set; }
    public JsonElement? StructuredData { get; set; }
    public List<string> Tags { get; set; } = [];
    public string SourceType { get; set; } = "agent";
    public string? SourceReference { get; set; }
    public List<string> SourceReferences { get; set; } = [];
    public required DateTimeOffset CreatedAtUtc { get; set; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? LastAccessedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset? ReviewAfterUtc { get; set; }
    public DateTimeOffset? LastReviewedAtUtc { get; set; }
    public ReviewState ReviewState { get; set; }
    public MemoryState Status { get; set; }
    public string? ReviewReason { get; set; }
    public List<string> ReviewEvidence { get; set; } = [];
    public required string ContentHash { get; set; }
    public long Revision { get; set; } = 1;
    public required string OperationId { get; set; }
    public bool Protected { get; set; }
    public long SuccessCount { get; set; }
    public long PartialSuccessCount { get; set; }
    public long FailureCount { get; set; }
    public long ContradictionCount { get; set; }
    public long UseCount { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }
    public DateTimeOffset? LastSuccessfulAtUtc { get; set; }
    public DateTimeOffset? LastFailedAtUtc { get; set; }
    public int IndependentConfirmations { get; set; }
    public List<MemoryConfirmation> SuccessfulConfirmations { get; set; } = [];
    public double Confidence { get; set; } = 0.5;
    public string? PromotionState { get; set; }
    public List<string> RelatedMemoryIds { get; set; } = [];
    public List<MemoryRelationship> Relationships { get; set; } = [];
    public List<MemoryProvenance> Provenance { get; set; } = [];
    public DateTimeOffset? ValidatedAtUtc { get; set; }
    public bool Stale { get; set; }
    public bool Contradicted { get; set; }
    public EmbeddingState EmbeddingState { get; set; }
    public VectorIndexState IndexState { get; set; }
    public EmbeddingMetadata? Embedding { get; set; }
    public CompactionDetails? Compaction { get; set; }
}
