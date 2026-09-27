using AgentSession.MCP.Services;

namespace AgentSession.MCP.Models.Memory;

public enum VectorMigrationState
{
    Prepared,
    AliasSwitched,
    Completed,
}

public sealed record VectorCollectionDescriptor(
    string RepositoryId,
    string Alias,
    string PhysicalCollection,
    EmbeddingFingerprint Fingerprint
);

public sealed class VectorMigrationIntent
{
    public int SchemaVersion { get; set; } = 1;
    public required string OperationId { get; set; }
    public required string RepositoryId { get; set; }
    public VectorCollectionDescriptor? Previous { get; set; }
    public string? PreviousPhysicalCollection { get; set; }
    public required VectorCollectionDescriptor Target { get; set; }
    public required long CatalogGeneration { get; set; }
    public required int ExpectedPoints { get; set; }
    public required VectorMigrationState State { get; set; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ActiveVectorCollection
{
    public int SchemaVersion { get; set; } = 1;
    public required VectorCollectionDescriptor Active { get; set; }
    public List<VectorCollectionDescriptor> Previous { get; set; } = [];
    public required DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed record VectorMigrationResult(
    string OperationId,
    string ActiveCollection,
    string? PreviousCollection,
    int IndexedPoints,
    bool Recovered
);
