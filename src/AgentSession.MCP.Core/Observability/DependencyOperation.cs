namespace AgentSession.MCP.Core.Observability;

public enum DependencyOperationKind
{
    External,
    Internal,
}

public sealed record DependencyOperation
{
    private DependencyOperation(
        DependencyOperationKind kind,
        string dependencyType,
        string operation,
        int? itemCount
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        Kind = kind;
        DependencyType = dependencyType;
        Operation = operation;
        ItemCount = itemCount;
    }

    public DependencyOperationKind Kind { get; }
    public string DependencyType { get; }
    public string Operation { get; }
    public int? ItemCount { get; }

    public static DependencyOperation External(
        string dependencyType,
        string operation,
        int? itemCount = null
    ) => new(DependencyOperationKind.External, dependencyType, operation, itemCount);

    public static DependencyOperation Internal(
        string dependencyType,
        string operation,
        int? itemCount = null
    ) => new(DependencyOperationKind.Internal, dependencyType, operation, itemCount);
}

public sealed record DependencyOperationMetadata
{
    public int? BatchItems { get; init; }
    public int? WorkDeleted { get; init; }
    public int? WorkArchived { get; init; }
    public int? WorkProcessed { get; init; }
    public int? WorkIndexed { get; init; }
    public int? WorkDeferred { get; init; }
    public int? WorkQueued { get; init; }
    public int? WorkOrphans { get; init; }
    public int? WorkErrors { get; init; }
    public bool? RecoveryPerformed { get; init; }
}
