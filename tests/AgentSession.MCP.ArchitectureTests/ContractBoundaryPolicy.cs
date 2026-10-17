namespace AgentSession.MCP.ArchitectureTests;

internal static class ContractBoundaryPolicy
{
    private static readonly string[] ForbiddenReferences =
    [
        "Microsoft.AspNetCore.",
        "ModelContextProtocol.",
        "OnuObservability.",
        "Qdrant.",
        "Ollama.",
        "System.IO.File",
        "System.IO.Directory",
        "AgentSession.MCP.Services.",
    ];

    public static IReadOnlyList<ArchitectureViolation> EvaluateForbiddenTypes(
        AssemblyContractMetadata metadata)
    {
        return metadata.TypeReferences
            .Where(type => ForbiddenReferences.Any(prefix => type.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(type => new ArchitectureViolation(
                "CONTRACT_FORBIDDEN_TYPE",
                metadata.AssemblyName,
                type,
                "Core/contracts reference a forbidden framework, provider, filesystem implementation, or service type."))
            .OrderBy(item => item.Target, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<ArchitectureViolation> EvaluateTypeCompatibility(
        IReadOnlyDictionary<string, AssemblyContractMetadata> targetAssemblies,
        AssemblyContractMetadata legacyAssembly,
        IReadOnlySet<string> expectedForwarders)
    {
        var violations = new List<ArchitectureViolation>();
        foreach (var fullName in expectedForwarders)
        {
            var definitionCount = targetAssemblies.Values.Count(item => item.PublicTypeDefinitions.Contains(fullName));
            if (definitionCount != 1)
                violations.Add(new("CONTRACT_TYPE_DEFINITION", fullName, definitionCount.ToString(), "Moved type must have exactly one target definition."));
            if (legacyAssembly.PublicTypeDefinitions.Contains(fullName))
                violations.Add(new("CONTRACT_LEGACY_DEFINITION", legacyAssembly.AssemblyName, fullName, "Legacy assembly must not define a moved type."));
            if (!legacyAssembly.ForwardedTypes.Contains(fullName))
                violations.Add(new("CONTRACT_FORWARDER_MISSING", legacyAssembly.AssemblyName, fullName, "Legacy assembly is missing an expected type forwarder."));
        }
        foreach (var extra in legacyAssembly.ForwardedTypes.Except(expectedForwarders, StringComparer.Ordinal))
            violations.Add(new("CONTRACT_FORWARDER_UNEXPECTED", legacyAssembly.AssemblyName, extra, "Legacy assembly contains an unapproved type forwarder."));
        return violations.OrderBy(item => item.Code, StringComparer.Ordinal).ThenBy(item => item.Target, StringComparer.Ordinal).ToArray();
    }
}
