using System.Collections.Frozen;
using AgentSession.MCP.AgentMemory.Contracts.Registration;
using AgentSession.MCP.Core.Execution;
using AgentSession.MCP.Session.Contracts.Registration;

namespace AgentSession.MCP.Protocol.Filters;

internal static class CapabilityCatalog
{
    private static readonly FrozenDictionary<string, CapabilityDescriptor> Descriptors =
        SessionModuleRegistrationDescriptor.Value.Capabilities
            .Concat(AgentMemoryModuleRegistrationDescriptor.Value.Capabilities)
            .ToFrozenDictionary(item => item.OperationId, StringComparer.Ordinal);

    public static bool TryGet(string operationId, out CapabilityDescriptor descriptor) =>
        Descriptors.TryGetValue(operationId, out descriptor!);
}
