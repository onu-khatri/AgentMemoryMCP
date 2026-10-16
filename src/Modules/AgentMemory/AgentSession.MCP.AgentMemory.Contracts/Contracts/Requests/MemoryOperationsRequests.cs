using System.ComponentModel.DataAnnotations;

namespace AgentSession.MCP.Contracts;

public sealed record RecordMemoryEventRequest(
    string EventId,
    string EventType,
    string AgentId,
    string? MemoryId = null,
    string? SessionId = null,
    string? TaskId = null,
    Dictionary<string, string>? Metadata = null
);

public enum MemoryReindexScope
{
    FileSystem,
    Qdrant,
    PendingEmbeddings,
    SpecificMemoryIds,
    ModelMigration,
    All,
}

public sealed record ReindexMemoryRequest(
    string OperationId,
    MemoryReindexScope Scope,
    [property: MaxLength(100)]
    List<string>? MemoryIds = null,
    [property: Range(1, 100)]
    int MaxItems = 100
);

public sealed record ReindexMemoryResult(
    MemoryReindexScope Scope,
    int InvalidCanonicalRecords,
    int QueuedVectors,
    int RemovedOrphanVectors,
    int ProcessedVectors,
    int IndexedVectors,
    int DeferredVectors,
    int Errors,
    string? ActiveCollection,
    bool ModelMigrated
);

public sealed record DependencyStatus(
    string State,
    string? ErrorCode = null,
    string? Identity = null
);

public sealed record MemoryStatusResult(
    string ServerVersion,
    string RepositoryId,
    string SystemRoot,
    string LearningRoot,
    int TempRecords,
    int ExpiredTempRecords,
    int ShortActiveRecords,
    int ShortReviewDueRecords,
    int ShortCompactedRecords,
    int ShortArchivedRecords,
    int LongCandidateRecords,
    int LongValidatedRecords,
    int PendingEmbeddingRecords,
    int RecoveryBacklog,
    DependencyStatus Ollama,
    DependencyStatus Qdrant,
    string? ActiveEmbeddingModel,
    string? ActiveVectorCollection,
    string IndexHealth,
    DateTimeOffset? LastCleanupAtUtc,
    DateTimeOffset? LastIndexRepairAtUtc,
    DateTimeOffset? LastEmbeddingRunAtUtc,
    DateTimeOffset? LastReconciliationAtUtc,
    DateTimeOffset? LastCompactionAtUtc,
    DateTimeOffset? LastArchiveAtUtc,
    int LastCleanupDeleted,
    int LastCleanupArchived,
    int LastMaintenanceErrors,
    int LastVectorsProcessed,
    int LastVectorsIndexed,
    int LastVectorsQueued,
    int LastOrphanVectorsRemoved
);
