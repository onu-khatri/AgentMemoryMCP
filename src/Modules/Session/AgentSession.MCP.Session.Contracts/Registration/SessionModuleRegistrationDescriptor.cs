using AgentSession.MCP.Core.Errors;
using AgentSession.MCP.Core.Execution;
using AgentSession.MCP.Core.Security;
using AgentSession.MCP.Session.Contracts.Capabilities;

namespace AgentSession.MCP.Session.Contracts.Registration;

public static class SessionModuleRegistrationDescriptor
{
    public static ModuleRegistrationDescriptor Value { get; } = new(
        "session",
        [typeof(ISessionLifecycleCapabilities), typeof(ISessionCoordinationCapabilities), typeof(ISessionResumeCapabilities)],
        [
            Capability("create_or_activate_session", CapabilityAccess.Write),
            Capability("list_agent_sessions", CapabilityAccess.Read),
            Capability("append_agent_memory", CapabilityAccess.Write),
            Capability("coordinate_agent_task", CapabilityAccess.Write),
            Capability("resume_agent_session", CapabilityAccess.Read),
            Capability("checkpoint_agent_session", CapabilityAccess.Write),
        ],
        ["managed-transaction-store", "configured-repository-resource", "canonical-session-storage"],
        Enum.GetValues<ErrorCategory>().ToHashSet(),
        ["module", "operation", "result.category", "dependency"],
        ["unit", "integration", "contract", "architecture", "end-to-end"]);

    private static CapabilityDescriptor Capability(string id, CapabilityAccess access) =>
        new(id, access, new(id, true, new HashSet<string>([Scope(access)], StringComparer.Ordinal), ResourceAccessRequirement.RepositoryBound));

    private static string Scope(CapabilityAccess access) => $"mcp:session:{access.ToString().ToLowerInvariant()}";
}
