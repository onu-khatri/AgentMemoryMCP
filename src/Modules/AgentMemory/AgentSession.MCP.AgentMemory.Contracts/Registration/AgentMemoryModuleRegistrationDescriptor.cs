using AgentSession.MCP.AgentMemory.Contracts.Capabilities;
using AgentSession.MCP.Core.Errors;
using AgentSession.MCP.Core.Execution;
using AgentSession.MCP.Core.Security;

namespace AgentSession.MCP.AgentMemory.Contracts.Registration;

public static class AgentMemoryModuleRegistrationDescriptor
{
    public static ModuleRegistrationDescriptor Value { get; } = new(
        "agent-memory",
        [typeof(IMemoryQueryCapabilities), typeof(IMemoryCommandCapabilities), typeof(IMemoryIndexCapabilities)],
        [
            Capability("memory_status", CapabilityAccess.Read),
            Capability("memory_remember", CapabilityAccess.Write),
            Capability("memory_get", CapabilityAccess.Read),
            Capability("memory_update", CapabilityAccess.Write),
            Capability("memory_recall", CapabilityAccess.Read),
            Capability("memory_delete", CapabilityAccess.Write),
            Capability("memory_archive", CapabilityAccess.Write),
            Capability("memory_prepare_compaction", CapabilityAccess.Write),
            Capability("memory_commit_compaction", CapabilityAccess.Write),
            Capability("memory_compact", CapabilityAccess.Write),
            Capability("memory_record_event", CapabilityAccess.Write),
            Capability("memory_record_outcome", CapabilityAccess.Write),
            Capability("memory_supersede", CapabilityAccess.Write),
            Capability("memory_retire", CapabilityAccess.Write),
            Capability("memory_promote", CapabilityAccess.Write),
            Capability("memory_list_review", CapabilityAccess.Read),
            Capability("memory_mark_reviewed", CapabilityAccess.Write),
            Capability("memory_cleanup", CapabilityAccess.Maintenance),
            Capability("memory_reindex", CapabilityAccess.Maintenance),
            Capability("memory_rebuild_indexes", CapabilityAccess.Maintenance),
        ],
        ["canonical-memory-storage", "embedding-provider", "vector-index"],
        Enum.GetValues<ErrorCategory>().ToHashSet(),
        ["module", "operation", "result.category", "dependency"],
        ["unit", "integration", "contract", "architecture", "end-to-end"]);

    private static CapabilityDescriptor Capability(string id, CapabilityAccess access) =>
        new(id, access, new(id, true, new HashSet<string>([Scope(access)], StringComparer.Ordinal), ResourceAccessRequirement.RepositoryBound));

    private static string Scope(CapabilityAccess access) => $"mcp:agent-memory:{access.ToString().ToLowerInvariant()}";
}
