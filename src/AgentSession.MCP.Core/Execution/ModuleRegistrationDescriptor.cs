using System.Collections.Frozen;
using System.Collections.ObjectModel;
using AgentSession.MCP.Core.Errors;
using AgentSession.MCP.Core.Security;

namespace AgentSession.MCP.Core.Execution;

public enum CapabilityAccess
{
    Read,
    Write,
    Maintenance,
}

public sealed record CapabilityDescriptor
{
    public CapabilityDescriptor(
        string operationId,
        CapabilityAccess access,
        AuthorizationRequirement authorization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(authorization);
        if (!string.Equals(operationId, authorization.CapabilityId, StringComparison.Ordinal))
            throw new ArgumentException("Authorization capability must match the operation ID.", nameof(authorization));
        OperationId = operationId;
        Access = access;
        Authorization = authorization;
    }

    public string OperationId { get; }
    public CapabilityAccess Access { get; }
    public AuthorizationRequirement Authorization { get; }
}

public sealed record ModuleRegistrationDescriptor
{
    public ModuleRegistrationDescriptor(
        string moduleId,
        IReadOnlyList<Type> capabilityContracts,
        IReadOnlyList<CapabilityDescriptor> capabilities,
        IReadOnlyList<string> externalDependencies,
        IReadOnlySet<ErrorCategory> failureCategories,
        IReadOnlyList<string> observabilityResponsibilities,
        IReadOnlyList<string> testLayers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(capabilityContracts);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(externalDependencies);
        ArgumentNullException.ThrowIfNull(failureCategories);
        ArgumentNullException.ThrowIfNull(observabilityResponsibilities);
        ArgumentNullException.ThrowIfNull(testLayers);
        if (capabilityContracts.Any(type => type is null || !type.IsInterface))
            throw new ArgumentException("Capability contracts must be interface types.", nameof(capabilityContracts));
        if (capabilities.Any(capability => capability is null))
            throw new ArgumentException("Capabilities cannot contain null values.", nameof(capabilities));
        EnsureNonblank(externalDependencies, nameof(externalDependencies));
        EnsureNonblank(observabilityResponsibilities, nameof(observabilityResponsibilities));
        EnsureNonblank(testLayers, nameof(testLayers));
        if (capabilities.Select(item => item.OperationId).Distinct(StringComparer.Ordinal).Count() != capabilities.Count)
            throw new ArgumentException("Capability operation IDs must be unique.", nameof(capabilities));

        ModuleId = moduleId;
        CapabilityContracts = ReadOnly(capabilityContracts);
        Capabilities = ReadOnly(capabilities);
        ExternalDependencies = ReadOnly(externalDependencies);
        FailureCategories = failureCategories.ToFrozenSet();
        ObservabilityResponsibilities = ReadOnly(observabilityResponsibilities);
        TestLayers = ReadOnly(testLayers);
    }

    public string ModuleId { get; }
    public IReadOnlyList<Type> CapabilityContracts { get; }
    public IReadOnlyList<CapabilityDescriptor> Capabilities { get; }
    public IReadOnlyList<string> ExternalDependencies { get; }
    public IReadOnlySet<ErrorCategory> FailureCategories { get; }
    public IReadOnlyList<string> ObservabilityResponsibilities { get; }
    public IReadOnlyList<string> TestLayers { get; }

    private static ReadOnlyCollection<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private static void EnsureNonblank(IEnumerable<string> values, string parameterName)
    {
        if (values.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Values cannot contain blank entries.", parameterName);
    }
}
