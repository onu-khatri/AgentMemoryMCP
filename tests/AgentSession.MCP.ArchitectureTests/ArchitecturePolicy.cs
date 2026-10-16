namespace AgentSession.MCP.ArchitectureTests;

internal sealed record ArchitectureViolation(string Code, string Source, string Target, string Message);

internal enum ProjectRole
{
    Production,
    LegacyExecutable,
    UnitTests,
    IntegrationTests,
    LegacyTests,
    TestWorker,
    Benchmark,
    OtherTest,
    ContractTests,
    Unknown,
}

internal static class ArchitecturePolicy
{
    private const string LegacyProject = "AgentSession.MCP/AgentSession.MCP.csproj";

    private static readonly StringComparer Comparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static readonly string[] ExpectedSolutionProjects =
    [
        "benchmarks/AgentSession.MCP.Benchmarks/AgentSession.MCP.Benchmarks.csproj",
        "src/AgentSession.MCP.Core/AgentSession.MCP.Core.csproj",
        "src/AgentSession.MCP.Host/AgentSession.MCP.Host.csproj",
        "src/AgentSession.MCP.Infrastructure/AgentSession.MCP.Infrastructure.csproj",
        "src/AgentSession.MCP.Protocol/AgentSession.MCP.Protocol.csproj",
        "src/AgentSession.MCP.ServiceDefaults/AgentSession.MCP.ServiceDefaults.csproj",
        "src/AgentSession.MCP.StdioHost/AgentSession.MCP.StdioHost.csproj",
        "src/Modules/AgentMemory/AgentSession.MCP.AgentMemory.Contracts/AgentSession.MCP.AgentMemory.Contracts.csproj",
        "src/Modules/AgentMemory/AgentSession.MCP.AgentMemory/AgentSession.MCP.AgentMemory.csproj",
        "src/Modules/Session/AgentSession.MCP.Session.Contracts/AgentSession.MCP.Session.Contracts.csproj",
        "src/Modules/Session/AgentSession.MCP.Session/AgentSession.MCP.Session.csproj",
        "tests/AgentSession.MCP.ArchitectureTests/AgentSession.MCP.ArchitectureTests.csproj",
        "tests/AgentSession.MCP.ContractTests/AgentSession.MCP.ContractTests.csproj",
        "tests/AgentSession.MCP.EndToEndTests/AgentSession.MCP.EndToEndTests.csproj",
        "tests/AgentSession.MCP.IntegrationTests/AgentSession.MCP.IntegrationTests.csproj",
        "tests/AgentSession.MCP.TestWorker/AgentSession.MCP.TestWorker.csproj",
        "tests/AgentSession.MCP.UnitTests/AgentSession.MCP.UnitTests.csproj"
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedProductionReferences =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["AgentSession.MCP.Core"] = Set(),
            ["AgentSession.MCP.Session.Contracts"] = Set("AgentSession.MCP.Core"),
            ["AgentSession.MCP.AgentMemory.Contracts"] = Set("AgentSession.MCP.Core"),
            ["AgentSession.MCP.Session"] = Set("AgentSession.MCP.Core", "AgentSession.MCP.Session.Contracts"),
            ["AgentSession.MCP.AgentMemory"] = Set("AgentSession.MCP.Core", "AgentSession.MCP.AgentMemory.Contracts", "Qdrant.Client"),
            ["AgentSession.MCP.Infrastructure"] = Set("AgentSession.MCP.Core"),
            ["AgentSession.MCP.Protocol"] = Set("AgentSession.MCP.Core", "AgentSession.MCP.Session.Contracts", "AgentSession.MCP.AgentMemory.Contracts"),
            ["AgentSession.MCP.ServiceDefaults"] = Set(),
            ["AgentSession.MCP.Host"] = Set("AgentSession.MCP.Core", "AgentSession.MCP.Protocol", "AgentSession.MCP.Session", "AgentSession.MCP.AgentMemory", "AgentSession.MCP.Infrastructure", "AgentSession.MCP.ServiceDefaults"),
            ["AgentSession.MCP.StdioHost"] = Set("AgentSession.MCP.Core", "AgentSession.MCP.Protocol", "AgentSession.MCP.Session", "AgentSession.MCP.AgentMemory", "AgentSession.MCP.Infrastructure", "AgentSession.MCP.ServiceDefaults")
        };

    private static readonly IReadOnlySet<string> CoreAndContractAssemblies = Set(
        "AgentSession.MCP.Core",
        "AgentSession.MCP.Session",
        "AgentSession.MCP.Session.Contracts",
        "AgentSession.MCP.AgentMemory.Contracts");

    private static readonly IReadOnlySet<string> LegacyCompatibilityAssemblies = Set(
        "AgentSession.MCP.Core",
        "AgentSession.MCP.Protocol",
        "AgentSession.MCP.Infrastructure",
        "AgentSession.MCP.Session",
        "AgentSession.MCP.Session.Contracts",
        "AgentSession.MCP.AgentMemory",
        "AgentSession.MCP.AgentMemory.Contracts");

    private static readonly IReadOnlySet<string> LegacyTestAssemblies = Set(
        "AgentSession.MCP",
        "AgentSession.MCP.Core",
        "AgentSession.MCP.Infrastructure",
        "AgentSession.MCP.Session.Contracts",
        "AgentSession.MCP.AgentMemory.Contracts");

    private static readonly IReadOnlySet<string> LegacyCompatibilityProjects = Set(
        "src/AgentSession.MCP.Core/AgentSession.MCP.Core.csproj",
        "src/AgentSession.MCP.Protocol/AgentSession.MCP.Protocol.csproj",
        "src/AgentSession.MCP.Infrastructure/AgentSession.MCP.Infrastructure.csproj",
        "src/Modules/AgentMemory/AgentSession.MCP.AgentMemory.Contracts/AgentSession.MCP.AgentMemory.Contracts.csproj",
        "src/Modules/AgentMemory/AgentSession.MCP.AgentMemory/AgentSession.MCP.AgentMemory.csproj",
        "src/Modules/Session/AgentSession.MCP.Session.Contracts/AgentSession.MCP.Session.Contracts.csproj",
        "src/Modules/Session/AgentSession.MCP.Session/AgentSession.MCP.Session.csproj");

    private static readonly IReadOnlySet<string> UnitTestProjects = Set(
        "src/AgentSession.MCP.Core/AgentSession.MCP.Core.csproj",
        "src/Modules/AgentMemory/AgentSession.MCP.AgentMemory.Contracts/AgentSession.MCP.AgentMemory.Contracts.csproj",
        "src/Modules/Session/AgentSession.MCP.Session.Contracts/AgentSession.MCP.Session.Contracts.csproj",
        "src/Modules/Session/AgentSession.MCP.Session/AgentSession.MCP.Session.csproj");

    private static readonly IReadOnlySet<string> IntegrationTestProjects = Set(
        "src/AgentSession.MCP.Core/AgentSession.MCP.Core.csproj",
        "src/AgentSession.MCP.Infrastructure/AgentSession.MCP.Infrastructure.csproj",
        "src/Modules/AgentMemory/AgentSession.MCP.AgentMemory/AgentSession.MCP.AgentMemory.csproj",
        "src/Modules/AgentMemory/AgentSession.MCP.AgentMemory.Contracts/AgentSession.MCP.AgentMemory.Contracts.csproj",
        "src/Modules/Session/AgentSession.MCP.Session.Contracts/AgentSession.MCP.Session.Contracts.csproj",
        "src/Modules/Session/AgentSession.MCP.Session/AgentSession.MCP.Session.csproj");

    private static readonly IReadOnlySet<string> LegacyTestProjects = Set(
        LegacyProject,
        "src/AgentSession.MCP.Core/AgentSession.MCP.Core.csproj",
        "src/AgentSession.MCP.Infrastructure/AgentSession.MCP.Infrastructure.csproj",
        "src/Modules/AgentMemory/AgentSession.MCP.AgentMemory.Contracts/AgentSession.MCP.AgentMemory.Contracts.csproj",
        "src/Modules/Session/AgentSession.MCP.Session.Contracts/AgentSession.MCP.Session.Contracts.csproj");

    private static readonly IReadOnlySet<string> TestPackages = Set(
        "coverlet.collector", "Microsoft.NET.Test.Sdk", "Microsoft.Extensions.Hosting", "xunit", "xunit.runner.visualstudio");

    private static readonly IReadOnlySet<string> InfrastructurePackages = Set(
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Options.ConfigurationExtensions",
        "OnuObservability.Hosting",
        "OnuObservability.Mcp");

    private static readonly IReadOnlySet<string> SessionPackages = Set(
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Options.ConfigurationExtensions");

    private static readonly IReadOnlySet<string> AgentMemoryPackages = Set(
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Hosting",
        "Microsoft.Extensions.Http",
        "Microsoft.Extensions.Options.ConfigurationExtensions",
        "Qdrant.Client");

    private static readonly IReadOnlySet<string> ProtocolPackages = Set("ModelContextProtocol");

    private static readonly IReadOnlySet<string> HostPackages = Set("ModelContextProtocol.AspNetCore");

    private static readonly IReadOnlySet<string> ServiceDefaultsPackages = Set(
        "OpenTelemetry.Exporter.OpenTelemetryProtocol", "OpenTelemetry.Extensions.Hosting");

    private static readonly IReadOnlySet<string> StdioHostPackages = Set(
        "Microsoft.Extensions.Configuration.CommandLine", "Microsoft.Extensions.Hosting", "Microsoft.Extensions.Http",
        "ModelContextProtocol", "OnuObservability.Hosting", "OnuObservability.Mcp");

    private static readonly IReadOnlySet<string> IntegrationTestPackages = Set(
        "coverlet.collector",
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Hosting",
        "Microsoft.NET.Test.Sdk",
        "OnuObservability.Hosting",
        "OnuObservability.Mcp",
        "OpenTelemetry.Exporter.InMemory",
        "Qdrant.Client",
        "xunit",
        "xunit.runner.visualstudio");

    private static readonly IReadOnlySet<string> TestWorkerPackages = Set(
        "Microsoft.Extensions.Hosting");

    public static IReadOnlyList<ArchitectureViolation> Evaluate(RepositoryGraph graph)
    {
        var violations = new List<ArchitectureViolation>();
        var actual = graph.SolutionProjects.ToHashSet(Comparer);
        var expected = ExpectedSolutionProjects.ToHashSet(Comparer);
        violations.AddRange(expected.Except(actual, Comparer).Select(path =>
            New("SOLUTION_INVENTORY_MISSING", "AgentMemoryMCP.slnx", path, "Required project is missing from the solution.")));
        violations.AddRange(actual.Except(expected, Comparer).Select(path =>
            New("SOLUTION_INVENTORY_UNEXPECTED", "AgentMemoryMCP.slnx", path, "Unexpected project is present in the solution.")));
        violations.AddRange(graph.MissingProjectFiles.Select(path =>
            New("PROJECT_FILE_MISSING", "AgentMemoryMCP.slnx", path, "Solution project file does not exist.")));
        violations.AddRange(EvaluateProjectReferences(graph));
        violations.AddRange(EvaluatePackageReferences(graph));
        violations.AddRange(EvaluateAssemblyReferences(graph));
        return Sort(violations);
    }

    public static IReadOnlyList<ArchitectureViolation> EvaluateProjectReferences(RepositoryGraph graph)
    {
        var projects = graph.Projects.ToDictionary(project => project.Path, project => project, Comparer);
        var violations = new List<ArchitectureViolation>();
        foreach (var edge in graph.ProjectReferences)
        {
            if (!projects.TryGetValue(edge.Source, out var source))
            {
                violations.Add(New("PROJECT_ROLE_UNKNOWN", edge.Source, edge.Target, "Project reference source has no known role."));
                continue;
            }

            projects.TryGetValue(edge.Target, out var target);
            violations.AddRange(EvaluateProjectEdge(source, edge.Target, target?.AssemblyName ?? KnownAssemblyName(edge.Target)));
        }

        foreach (var project in graph.Projects)
            violations.AddRange(EvaluateExactProjectSet(project, graph.ProjectReferences
                .Where(edge => Comparer.Equals(edge.Source, project.Path))
                .Select(edge => edge.Target)
                .ToHashSet(Comparer)));
        return Sort(violations);
    }

    public static IReadOnlyList<ArchitectureViolation> EvaluateProjectEdge(
        ProjectNode source,
        string targetPath,
        string? targetAssembly)
    {
        var role = Classify(source);
        var allowed = role switch
        {
            ProjectRole.Production when AllowedProductionReferences.TryGetValue(source.AssemblyName, out var production) => production,
            ProjectRole.LegacyExecutable => LegacyCompatibilityAssemblies,
            ProjectRole.UnitTests => CoreAndContractAssemblies,
            ProjectRole.IntegrationTests => Set("AgentSession.MCP.Core", "AgentSession.MCP.Infrastructure", "AgentSession.MCP.Session.Contracts", "AgentSession.MCP.Session", "AgentSession.MCP.AgentMemory.Contracts", "AgentSession.MCP.AgentMemory"),
            ProjectRole.LegacyTests => LegacyTestAssemblies,
            ProjectRole.TestWorker => Set("AgentSession.MCP.Core", "AgentSession.MCP.Infrastructure", "AgentSession.MCP.Session.Contracts", "AgentSession.MCP.Session"),
            ProjectRole.Benchmark => Set("AgentSession.MCP", "AgentSession.MCP.Core"),
            ProjectRole.OtherTest => Set(),
            ProjectRole.ContractTests => Set("AgentSession.MCP.Protocol"),
            _ => null,
        };
        if (allowed is not null && targetAssembly is not null && allowed.Contains(targetAssembly))
            return [];
        var code = role == ProjectRole.Unknown ? "PROJECT_ROLE_UNKNOWN" : "PROJECT_EDGE";
        return [New(code, source.Path, targetPath, "Project reference is outside the approved role-specific matrix.")];
    }

    public static IReadOnlyList<ArchitectureViolation> EvaluatePackageReferences(RepositoryGraph graph)
    {
        var projects = graph.Projects.ToDictionary(project => project.Path, project => project, Comparer);
        var violations = new List<ArchitectureViolation>();
        foreach (var edge in graph.PackageReferences)
        {
            projects.TryGetValue(edge.Source, out var source);
            var role = source is null ? ProjectRole.Unknown : Classify(source);
            var allowedPackages = role switch
            {
                ProjectRole.Production when source?.AssemblyName == "AgentSession.MCP.Infrastructure" => InfrastructurePackages,
                ProjectRole.Production when source?.AssemblyName == "AgentSession.MCP.Session" => SessionPackages,
                ProjectRole.Production when source?.AssemblyName == "AgentSession.MCP.AgentMemory" => AgentMemoryPackages,
                ProjectRole.Production when source?.AssemblyName == "AgentSession.MCP.Protocol" => ProtocolPackages,
                ProjectRole.Production when source?.AssemblyName == "AgentSession.MCP.Host" => HostPackages,
                ProjectRole.Production when source?.AssemblyName == "AgentSession.MCP.ServiceDefaults" => ServiceDefaultsPackages,
                ProjectRole.Production when source?.AssemblyName == "AgentSession.MCP.StdioHost" => StdioHostPackages,
                ProjectRole.ContractTests => TestPackages,
                ProjectRole.IntegrationTests => IntegrationTestPackages,
                ProjectRole.TestWorker => TestWorkerPackages,
                _ when IsTestRole(role) => TestPackages,
                _ => null,
            };
            var allowed = allowedPackages?.Contains(edge.Package) == true;
            if (!allowed)
                violations.Add(New("PACKAGE_OWNER", edge.Source, edge.Package, "Direct package is not permitted for this project role."));
        }
        return Sort(violations);
    }

    public static IReadOnlyList<ArchitectureViolation> EvaluateAssemblyReferences(RepositoryGraph graph) =>
        Sort(graph.AssemblyReferences.SelectMany(EvaluateAssemblyEdge));

    public static IReadOnlyList<ArchitectureViolation> EvaluateAssemblyEdge(AssemblyReferenceEdge edge)
    {
        if (!IsGovernedAssemblyTarget(edge.Target))
            return [];
        var role = ClassifyAssembly(edge.Source);
        var allowed = role switch
        {
            ProjectRole.Production when AllowedProductionReferences.TryGetValue(edge.Source, out var production) => production,
            ProjectRole.LegacyExecutable => LegacyCompatibilityAssemblies,
            ProjectRole.UnitTests => CoreAndContractAssemblies,
            ProjectRole.IntegrationTests => Set(
                "AgentSession.MCP.Core", "AgentSession.MCP.Infrastructure", "AgentSession.MCP.Session.Contracts",
                "AgentSession.MCP.Session", "AgentSession.MCP.AgentMemory.Contracts", "AgentSession.MCP.AgentMemory"),
            ProjectRole.LegacyTests => LegacyTestAssemblies,
            ProjectRole.TestWorker => Set("AgentSession.MCP.Core", "AgentSession.MCP.Infrastructure", "AgentSession.MCP.Session.Contracts", "AgentSession.MCP.Session"),
            ProjectRole.Benchmark => Set("AgentSession.MCP", "AgentSession.MCP.Core"),
            ProjectRole.OtherTest => Set(),
            ProjectRole.ContractTests => Set("src/AgentSession.MCP.Protocol/AgentSession.MCP.Protocol.csproj"),
            _ => null,
        };
        if (allowed is not null && allowed.Contains(edge.Target))
            return [];
        var code = role == ProjectRole.Unknown ? "ASSEMBLY_ROLE_UNKNOWN" : "ASSEMBLY_EDGE";
        return [New(code, edge.Source, edge.Target, "Compiled assembly reference is outside the approved role-specific matrix.")];
    }

    public static IReadOnlyList<ArchitectureViolation> EvaluateExactProjectSet(
        ProjectNode source,
        IReadOnlySet<string> actual)
    {
        var role = Classify(source);
        var expected = role switch
        {
            ProjectRole.LegacyExecutable => LegacyCompatibilityProjects,
            ProjectRole.UnitTests => UnitTestProjects,
            ProjectRole.IntegrationTests => IntegrationTestProjects,
            ProjectRole.LegacyTests => LegacyTestProjects,
            ProjectRole.TestWorker => Set(
                "src/AgentSession.MCP.Core/AgentSession.MCP.Core.csproj",
                "src/AgentSession.MCP.Infrastructure/AgentSession.MCP.Infrastructure.csproj",
                "src/Modules/Session/AgentSession.MCP.Session.Contracts/AgentSession.MCP.Session.Contracts.csproj",
                "src/Modules/Session/AgentSession.MCP.Session/AgentSession.MCP.Session.csproj"),
            ProjectRole.Benchmark => Set(
                LegacyProject,
                "src/AgentSession.MCP.Core/AgentSession.MCP.Core.csproj"),
            _ => null,
        };
        if (expected is null)
            return [];
        var prefix = role switch
        {
            ProjectRole.LegacyExecutable => "LEGACY_COMPATIBILITY",
            ProjectRole.UnitTests => "UNIT_TEST_COMPILE",
            ProjectRole.IntegrationTests => "INTEGRATION_TEST_COMPILE",
            ProjectRole.LegacyTests => "LEGACY_TEST_COMPILE",
            _ => "TRANSITION_EXCEPTION",
        };
        return Sort(expected.Except(actual, Comparer)
            .Select(target => New(prefix + "_MISSING", source.Path, target, "Required role-specific reference is missing."))
            .Concat(actual.Except(expected, Comparer)
                .Select(target => New(prefix + "_UNEXPECTED", source.Path, target, "Unapproved role-specific reference exists."))));
    }

    private static ProjectRole Classify(ProjectNode project)
    {
        if (Comparer.Equals(project.Path, LegacyProject)) return ProjectRole.LegacyExecutable;
        if (Comparer.Equals(project.Path, "tests/AgentSession.MCP.UnitTests/AgentSession.MCP.UnitTests.csproj")) return ProjectRole.UnitTests;
        if (Comparer.Equals(project.Path, "tests/AgentSession.MCP.IntegrationTests/AgentSession.MCP.IntegrationTests.csproj")) return ProjectRole.IntegrationTests;
        if (Comparer.Equals(project.Path, "tests/AgentSession.MCP.ContractTests/AgentSession.MCP.ContractTests.csproj")) return ProjectRole.ContractTests;
        if (Comparer.Equals(project.Path, "AgentSession.MCP.Tests/AgentSession.MCP.Tests.csproj")) return ProjectRole.LegacyTests;
        if (Comparer.Equals(project.Path, "tests/AgentSession.MCP.TestWorker/AgentSession.MCP.TestWorker.csproj")) return ProjectRole.TestWorker;
        if (Comparer.Equals(project.Path, "benchmarks/AgentSession.MCP.Benchmarks/AgentSession.MCP.Benchmarks.csproj")) return ProjectRole.Benchmark;
        if (AllowedProductionReferences.ContainsKey(project.AssemblyName)) return ProjectRole.Production;
        if (project.Path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase)) return ProjectRole.OtherTest;
        return ProjectRole.Unknown;
    }

    private static ProjectRole ClassifyAssembly(string assemblyName)
    {
        if (assemblyName == "AgentSession.MCP") return ProjectRole.LegacyExecutable;
        if (assemblyName == "AgentSession.MCP.UnitTests") return ProjectRole.UnitTests;
        if (assemblyName == "AgentSession.MCP.IntegrationTests") return ProjectRole.IntegrationTests;
        if (assemblyName == "AgentSession.MCP.Tests") return ProjectRole.LegacyTests;
        if (assemblyName == "AgentSession.MCP.TestWorker") return ProjectRole.TestWorker;
        if (assemblyName == "AgentSession.MCP.Benchmarks") return ProjectRole.Benchmark;
        if (AllowedProductionReferences.ContainsKey(assemblyName)) return ProjectRole.Production;
        if (assemblyName.EndsWith("Tests", StringComparison.Ordinal)) return ProjectRole.OtherTest;
        return ProjectRole.Unknown;
    }

    private static bool IsGovernedAssemblyTarget(string target) =>
        target == "AgentSession.MCP" ||
        AllowedProductionReferences.ContainsKey(target) ||
        target.StartsWith("Qdrant", StringComparison.Ordinal) ||
        target.StartsWith("Ollama", StringComparison.Ordinal);

    private static bool IsTestRole(ProjectRole role) => role is
        ProjectRole.UnitTests or ProjectRole.IntegrationTests or ProjectRole.LegacyTests or ProjectRole.OtherTest or ProjectRole.ContractTests;

    private static string? KnownAssemblyName(string path) => path switch
    {
        LegacyProject => "AgentSession.MCP",
        _ => null,
    };

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.OrdinalIgnoreCase);
    private static ArchitectureViolation New(string code, string source, string target, string message) => new(code, source, target, message);
    private static IReadOnlyList<ArchitectureViolation> Sort(IEnumerable<ArchitectureViolation> violations) =>
        violations.OrderBy(violation => violation.Code, StringComparer.Ordinal)
            .ThenBy(violation => violation.Source, Comparer)
            .ThenBy(violation => violation.Target, Comparer)
            .ToArray();
}
