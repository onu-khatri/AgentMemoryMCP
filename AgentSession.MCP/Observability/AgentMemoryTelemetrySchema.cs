using System.Collections.Immutable;
using System.Diagnostics;
using OnuObservability.Schema;

namespace AgentSession.MCP.Observability;

internal static class AgentMemoryTelemetrySchema
{
    private static readonly string[] DependencyTypes =
        ["ollama", "qdrant", "filesystem", "maintenance", "other"];

    private static readonly string[] DependencyOperations =
    [
        "model_discovery", "embedding", "health", "collection", "migration",
        "query", "upsert", "delete", "lock", "transaction", "recovery",
        "maintenance", "reconciliation", "reindex", "other",
    ];

    public static readonly TelemetryFieldDescriptor DependencyType =
        TelemetryFieldDescriptor.FiniteString(
            "dependency.type", DependencyTypes, "other", isRequired: true);

    public static readonly TelemetryFieldDescriptor DependencyOperation =
        TelemetryFieldDescriptor.FiniteString(
            "dependency.operation", DependencyOperations, "other", isRequired: true);

    public static readonly TelemetryFieldDescriptor BatchItems =
        TelemetryFieldDescriptor.Count("mcp.batch.items", 1_024);

    public static readonly TelemetryFieldDescriptor WorkDeleted = Work("mcp.work.deleted");
    public static readonly TelemetryFieldDescriptor WorkArchived = Work("mcp.work.archived");
    public static readonly TelemetryFieldDescriptor WorkProcessed = Work("mcp.work.processed");
    public static readonly TelemetryFieldDescriptor WorkIndexed = Work("mcp.work.indexed");
    public static readonly TelemetryFieldDescriptor WorkDeferred = Work("mcp.work.deferred");
    public static readonly TelemetryFieldDescriptor WorkQueued = Work("mcp.work.queued");
    public static readonly TelemetryFieldDescriptor WorkOrphans = Work("mcp.work.orphans");
    public static readonly TelemetryFieldDescriptor WorkErrors = Work("mcp.work.errors");
    public static readonly TelemetryFieldDescriptor RecoveryPerformed =
        TelemetryFieldDescriptor.Flag("mcp.recovery.performed");

    private static readonly ImmutableDictionary<(ActivityKind Kind, string Type, string Operation), OperationDescriptor>
        OperationMap = CreateOperations();

    public static IEnumerable<OperationDescriptor> Operations => OperationMap.Values;

    public static (OperationDescriptor Descriptor, string Type, string Operation) Resolve(
        ActivityKind kind,
        string? dependencyType,
        string? dependencyOperation)
    {
        var type = Normalize(DependencyType, dependencyType);
        var operation = Normalize(DependencyOperation, dependencyOperation);
        if (!OperationMap.TryGetValue((kind, type, operation), out var descriptor))
        {
            type = "other";
            operation = "other";
            descriptor = OperationMap[(kind, type, operation)];
        }

        return (descriptor, type, operation);
    }

    private static ImmutableDictionary<(ActivityKind, string, string), OperationDescriptor>
        CreateOperations()
    {
        var entries = new List<((ActivityKind, string, string) Key, OperationDescriptor Value)>();
        Add(entries, ActivityKind.Client, "ollama", "model_discovery");
        Add(entries, ActivityKind.Client, "ollama", "embedding");
        foreach (var operation in new[]
                 { "health", "collection", "migration", "query", "upsert", "delete" })
        {
            Add(entries, ActivityKind.Client, "qdrant", operation);
        }

        foreach (var operation in new[] { "lock", "transaction", "recovery" })
        {
            Add(entries, ActivityKind.Internal, "filesystem", operation);
        }

        foreach (var operation in new[] { "migration", "maintenance", "reconciliation", "reindex" })
        {
            Add(entries, ActivityKind.Internal, "maintenance", operation);
        }

        Add(entries, ActivityKind.Client, "other", "other");
        Add(entries, ActivityKind.Internal, "other", "other");
        return entries.ToImmutableDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static void Add(
        ICollection<((ActivityKind, string, string) Key, OperationDescriptor Value)> entries,
        ActivityKind kind,
        string type,
        string operation)
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
                ])));
    }

    private static string Normalize(TelemetryFieldDescriptor field, string? value) =>
        value is not null && field.AllowedValues.Contains(value, StringComparer.Ordinal)
            ? value
            : field.FallbackValue!;

    private static TelemetryFieldDescriptor Work(string name) =>
        TelemetryFieldDescriptor.Count(name, 1_024);
}
