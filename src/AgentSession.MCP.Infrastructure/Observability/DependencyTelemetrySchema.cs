using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using OnuObservability.Schema;

namespace AgentSession.MCP.Infrastructure.Observability;

public static class DependencyTelemetrySchema
{
    public const string ActivitySourceName = "AgentSession.MCP";
    public const string Other = "other";

    private static readonly string[] DependencyTypes =
        ["ollama", "qdrant", "filesystem", "maintenance", Other];

    private static readonly string[] DependencyOperations =
    [
        "model_discovery",
        "embedding",
        "health",
        "collection",
        "migration",
        "query",
        "upsert",
        "delete",
        "lock",
        "transaction",
        "recovery",
        "maintenance",
        "reconciliation",
        "reindex",
        Other,
    ];

    private static readonly TelemetryFieldDescriptor DependencyType =
        TelemetryFieldDescriptor.FiniteString(
            "dependency.type",
            DependencyTypes,
            Other,
            isRequired: true
        );

    private static readonly TelemetryFieldDescriptor DependencyOperation =
        TelemetryFieldDescriptor.FiniteString(
            "dependency.operation",
            DependencyOperations,
            Other,
            isRequired: true
        );

    private static readonly TelemetryFieldDescriptor BatchItems =
        TelemetryFieldDescriptor.Count("mcp.batch.items", 1_024);

    private static readonly TelemetryFieldDescriptor WorkDeleted = Work("mcp.work.deleted");
    private static readonly TelemetryFieldDescriptor WorkArchived = Work("mcp.work.archived");
    private static readonly TelemetryFieldDescriptor WorkProcessed = Work("mcp.work.processed");
    private static readonly TelemetryFieldDescriptor WorkIndexed = Work("mcp.work.indexed");
    private static readonly TelemetryFieldDescriptor WorkDeferred = Work("mcp.work.deferred");
    private static readonly TelemetryFieldDescriptor WorkQueued = Work("mcp.work.queued");
    private static readonly TelemetryFieldDescriptor WorkOrphans = Work("mcp.work.orphans");
    private static readonly TelemetryFieldDescriptor WorkErrors = Work("mcp.work.errors");
    private static readonly TelemetryFieldDescriptor RecoveryPerformed =
        TelemetryFieldDescriptor.Flag("mcp.recovery.performed");

    private static readonly ImmutableDictionary<
        (ActivityKind Kind, string Type, string Operation),
        OperationDescriptor
    > OperationMap = CreateOperations();

    public static ActivitySource Activities { get; } = new(ActivitySourceName);

    public static IEnumerable<OperationDescriptor> Operations => OperationMap.Values;

    public static FrozenSet<string> ErrorCodes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "approval_required",
        "capacity_exceeded",
        "claim_conflict",
        "client_cancelled",
        "content_policy_rejected",
        "deadline_exceeded",
        "dependency_grpc_error",
        "dependency_http_error",
        "dependency_error",
        "event_type_reserved",
        "index_incompatible",
        "index_incomplete",
        "internal_error",
        "invalid_request",
        "lock_timeout",
        "mcp_error",
        "not_found",
        "operation_conflict",
        "policy_denied",
        "protocol_error",
        "review_required",
        "revision_conflict",
        "source_coverage_incomplete",
        "stale_claim",
        "storage_error",
        "storage_invalid",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static (OperationDescriptor Descriptor, string Type, string Operation) Resolve(
        ActivityKind kind,
        string? dependencyType,
        string? dependencyOperation
    )
    {
        var type = Normalize(DependencyType, dependencyType);
        var operation = Normalize(DependencyOperation, dependencyOperation);
        if (!OperationMap.TryGetValue((kind, type, operation), out var descriptor))
        {
            type = Other;
            operation = Other;
            descriptor = OperationMap[(kind, type, operation)];
        }

        return (descriptor, type, operation);
    }

    public static string OutcomeName(DependencyTelemetryOutcome outcome) =>
        string.Concat(
            outcome
                .ToString()
                .Select((character, index) =>
                    char.IsUpper(character) && index > 0
                        ? "_" + char.ToLowerInvariant(character)
                        : char.ToLowerInvariant(character).ToString()
                )
        );

    public static string NormalizeErrorCode(string? code) =>
        code is not null && ErrorCodes.Contains(code) ? code : Other;

    private static ImmutableDictionary<(ActivityKind, string, string), OperationDescriptor>
        CreateOperations()
    {
        var entries = new List<((ActivityKind, string, string) Key, OperationDescriptor Value)>();
        Add(entries, ActivityKind.Client, "ollama", "model_discovery");
        Add(entries, ActivityKind.Client, "ollama", "embedding");
        foreach (var operation in new[]
                 { "health", "collection", "migration", "query", "upsert", "delete" })
            Add(entries, ActivityKind.Client, "qdrant", operation);

        foreach (var operation in new[] { "lock", "transaction", "recovery" })
            Add(entries, ActivityKind.Internal, "filesystem", operation);

        foreach (var operation in new[] { "migration", "maintenance", "reconciliation", "reindex" })
            Add(entries, ActivityKind.Internal, "maintenance", operation);

        Add(entries, ActivityKind.Client, Other, Other);
        Add(entries, ActivityKind.Internal, Other, Other);
        return entries.ToImmutableDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static void Add(
        ICollection<((ActivityKind, string, string) Key, OperationDescriptor Value)> entries,
        ActivityKind kind,
        string type,
        string operation
    )
    {
        var boundary = kind == ActivityKind.Client ? "dependency" : "operation";
        entries.Add((
            (kind, type, operation),
            new OperationDescriptor(
                $"agentmemory.{boundary}.{type}.{operation}",
                [
                    DependencyType,
                    DependencyOperation,
                    BatchItems,
                    WorkDeleted,
                    WorkArchived,
                    WorkProcessed,
                    WorkIndexed,
                    WorkDeferred,
                    WorkQueued,
                    WorkOrphans,
                    WorkErrors,
                    RecoveryPerformed,
                ]
            )
        ));
    }

    private static string Normalize(TelemetryFieldDescriptor field, string? value) =>
        value is not null && field.AllowedValues.Contains(value, StringComparer.Ordinal)
            ? value
            : field.FallbackValue!;

    private static TelemetryFieldDescriptor Work(string name) =>
        TelemetryFieldDescriptor.Count(name, 1_024);
}

public enum DependencyTelemetryOutcome
{
    Success,
    ValidationError,
    AuthorizationDenied,
    ClientCancelled,
    DeadlineExceeded,
    DependencyError,
    ToolError,
    ProtocolError,
    InternalError,
}
