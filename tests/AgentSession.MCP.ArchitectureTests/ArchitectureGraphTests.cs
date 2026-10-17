namespace AgentSession.MCP.ArchitectureTests;

public sealed class ArchitectureGraphTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Real_solution_inventory_is_exact()
    {
        var graph = RepositoryGraph.Load(RepositoryRoot);
        var violations = ArchitecturePolicy.Evaluate(graph)
            .Where(violation => violation.Code.StartsWith("SOLUTION_", StringComparison.Ordinal) || violation.Code == "PROJECT_FILE_MISSING");
        AssertNoViolations(violations);
    }

    [Fact]
    public void Real_production_project_graph_is_allowed()
    {
        var graph = RepositoryGraph.Load(RepositoryRoot);
        AssertNoViolations(ArchitecturePolicy.EvaluateProjectReferences(graph));
    }

    [Fact]
    public void Real_package_ownership_is_allowed()
    {
        var graph = RepositoryGraph.Load(RepositoryRoot);
        AssertNoViolations(ArchitecturePolicy.EvaluatePackageReferences(graph));
    }

    [Fact]
    public void Real_compiled_assemblies_have_no_forbidden_references()
    {
        var graph = RepositoryGraph.Load(RepositoryRoot);
        Assert.NotEmpty(graph.AssemblyReferences);
        AssertNoViolations(ArchitecturePolicy.EvaluateAssemblyReferences(graph));
    }

    [Fact]
    public void Invalid_missing_project_fixture_is_rejected_for_expected_rule()
    {
        var graph = RepositoryGraph.Load(Fixture("invalid-missing-project"));
        Assert.Contains(ArchitecturePolicy.Evaluate(graph), violation => violation.Code == "SOLUTION_INVENTORY_MISSING");
    }

    [Fact]
    public void Invalid_legacy_entry_fixture_is_rejected_for_expected_rule()
    {
        var graph = RepositoryGraph.Load(Fixture("invalid-legacy-entry"));
        Assert.Contains(ArchitecturePolicy.Evaluate(graph), violation => violation.Code == "SOLUTION_INVENTORY_UNEXPECTED");
    }

    [Fact]
    public void Invalid_forbidden_edge_fixture_is_rejected_for_expected_rule()
    {
        var root = Fixture("invalid-forbidden-edge");
        var graph = RepositoryGraph.LoadProjects(root, ["Source/Source.csproj", "Target/Target.csproj"], []);
        Assert.Contains(ArchitecturePolicy.EvaluateProjectReferences(graph), violation => violation.Code == "PROJECT_EDGE");
    }

    [Fact]
    public void Session_to_AgentMemory_implementation_fixture_is_rejected_for_project_and_assembly_rules()
    {
        var root = Fixture("invalid-session-agentmemory");
        var graph = RepositoryGraph.LoadProjects(
            root,
            ["Session/Session.csproj", "AgentMemory/AgentMemory.csproj"],
            []
        );
        Assert.Contains(
            ArchitecturePolicy.EvaluateProjectReferences(graph),
            violation => violation.Code == "PROJECT_EDGE"
                && violation.Source == "Session/Session.csproj"
                && violation.Target == "AgentMemory/AgentMemory.csproj"
        );
        Assert.Contains(
            ArchitecturePolicy.EvaluateAssemblyEdge(
                new("AgentSession.MCP.Session", "AgentSession.MCP.AgentMemory")
            ),
            violation => violation.Code == "ASSEMBLY_EDGE"
        );
    }

    [Fact]
    public void Invalid_core_package_fixture_is_rejected_for_expected_rule()
    {
        var root = Fixture("invalid-core-package");
        var graph = RepositoryGraph.LoadProjects(root, ["Core.csproj"], []);
        Assert.Contains(ArchitecturePolicy.EvaluatePackageReferences(graph), violation => violation.Code == "PACKAGE_OWNER");
    }

    [Fact]
    public void Transitional_exceptions_are_exactly_bounded()
    {
        var graph = RepositoryGraph.Load(RepositoryRoot);
        var violations = ArchitecturePolicy.Evaluate(graph)
            .Where(violation => violation.Code.StartsWith("TRANSITION_EXCEPTION_", StringComparison.Ordinal));
        AssertNoViolations(violations);
    }

    [Fact]
    public void Real_core_and_contract_assemblies_have_no_forbidden_type_references()
    {
        foreach (var path in TargetAssemblyPaths())
            AssertNoViolations(ContractBoundaryPolicy.EvaluateForbiddenTypes(
                RepositoryGraph.ReadAssemblyContractMetadata(path)));
    }

    private static string[] TargetAssemblyPaths() =>
    [
        Path.Combine(RepositoryRoot, "src", "AgentSession.MCP.Core", "bin", "Release", "net10.0", "AgentSession.MCP.Core.dll"),
        Path.Combine(RepositoryRoot, "src", "Modules", "Session", "AgentSession.MCP.Session.Contracts", "bin", "Release", "net10.0", "AgentSession.MCP.Session.Contracts.dll"),
        Path.Combine(RepositoryRoot, "src", "Modules", "AgentMemory", "AgentSession.MCP.AgentMemory.Contracts", "bin", "Release", "net10.0", "AgentSession.MCP.AgentMemory.Contracts.dll"),
    ];

    [Fact]
    public void Real_role_specific_project_references_are_exactly_bounded()
    {
        var solutionGraph = RepositoryGraph.Load(RepositoryRoot);
        AssertNoViolations(ArchitecturePolicy.EvaluateProjectReferences(solutionGraph));
        Assert.Equal(4, solutionGraph.ProjectReferences.Count(edge =>
            edge.Source == "tests/AgentSession.MCP.UnitTests/AgentSession.MCP.UnitTests.csproj"));
        Assert.Equal(6, solutionGraph.ProjectReferences.Count(edge =>
            edge.Source == "tests/AgentSession.MCP.IntegrationTests/AgentSession.MCP.IntegrationTests.csproj"));

        Assert.False(File.Exists(Path.Combine(RepositoryRoot, "AgentSession.MCP/AgentSession.MCP.csproj")));
    }

    [Theory]
    [InlineData("AgentSession.MCP.Host")]
    [InlineData("AgentSession.MCP.StdioHost")]
    [InlineData("AgentSession.MCP.Protocol")]
    [InlineData("AgentSession.MCP.Infrastructure")]
    [InlineData("AgentSession.MCP.ServiceDefaults")]
    [InlineData("Qdrant.Client")]
    public void Unit_tests_reject_unapproved_host_implementation_and_provider_edges(string targetAssembly)
    {
        var source = new ProjectNode(
            "tests/AgentSession.MCP.UnitTests/AgentSession.MCP.UnitTests.csproj",
            "AgentSession.MCP.UnitTests");
        Assert.Contains(
            ArchitecturePolicy.EvaluateProjectEdge(source, targetAssembly + ".csproj", targetAssembly),
            violation => violation.Code == "PROJECT_EDGE");
        Assert.Contains(
            ArchitecturePolicy.EvaluateAssemblyEdge(new("AgentSession.MCP.UnitTests", targetAssembly)),
            violation => violation.Code == "ASSEMBLY_EDGE");
    }

    [Theory]
    [InlineData("AgentSession.MCP.Host")]
    [InlineData("AgentSession.MCP.StdioHost")]
    [InlineData("AgentSession.MCP.Protocol")]
    [InlineData("Qdrant.Client")]
    public void Integration_tests_reject_module_host_protocol_and_provider_edges(
        string targetAssembly
    )
    {
        var source = new ProjectNode(
            "tests/AgentSession.MCP.IntegrationTests/AgentSession.MCP.IntegrationTests.csproj",
            "AgentSession.MCP.IntegrationTests"
        );
        Assert.Contains(
            ArchitecturePolicy.EvaluateProjectEdge(
                source,
                targetAssembly + ".csproj",
                targetAssembly
            ),
            violation => violation.Code == "PROJECT_EDGE"
        );
        Assert.Contains(
            ArchitecturePolicy.EvaluateAssemblyEdge(
                new("AgentSession.MCP.IntegrationTests", targetAssembly)
            ),
            violation => violation.Code == "ASSEMBLY_EDGE"
        );
    }

    [Theory]
    [InlineData("AgentSession.MCP.Protocol")]
    [InlineData("AgentSession.MCP.Host")]
    public void Infrastructure_rejects_protocol_and_host_edges(string targetAssembly)
    {
        var source = new ProjectNode(
            "src/AgentSession.MCP.Infrastructure/AgentSession.MCP.Infrastructure.csproj",
            "AgentSession.MCP.Infrastructure"
        );
        Assert.Contains(
            ArchitecturePolicy.EvaluateProjectEdge(
                source,
                targetAssembly + ".csproj",
                targetAssembly
            ),
            violation => violation.Code == "PROJECT_EDGE"
        );
        Assert.Contains(
            ArchitecturePolicy.EvaluateAssemblyEdge(
                new("AgentSession.MCP.Infrastructure", targetAssembly)
            ),
            violation => violation.Code == "ASSEMBLY_EDGE"
        );
    }

    [Fact]
    public void Unknown_project_role_is_rejected()
    {
        var source = new ProjectNode("custom/Unknown.csproj", "Unknown");
        Assert.Contains(
            ArchitecturePolicy.EvaluateProjectEdge(source, "src/AgentSession.MCP.Core/AgentSession.MCP.Core.csproj", "AgentSession.MCP.Core"),
            violation => violation.Code == "PROJECT_ROLE_UNKNOWN");
    }

    [Fact]
    public void Missing_and_extra_test_compile_edges_are_rejected()
    {
        var source = new ProjectNode(
            "tests/AgentSession.MCP.UnitTests/AgentSession.MCP.UnitTests.csproj",
            "AgentSession.MCP.UnitTests");
        var incomplete = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "src/AgentSession.MCP.Core/AgentSession.MCP.Core.csproj",
        };
        Assert.Contains(
            ArchitecturePolicy.EvaluateExactProjectSet(source, incomplete),
            violation => violation.Code == "UNIT_TEST_COMPILE_MISSING");
        incomplete.Add("src/AgentSession.MCP.Host/AgentSession.MCP.Host.csproj");
        Assert.Contains(
            ArchitecturePolicy.EvaluateExactProjectSet(source, incomplete),
            violation => violation.Code == "UNIT_TEST_COMPILE_UNEXPECTED");
    }

    [Fact]
    public void Missing_forwarder_is_rejected_for_expected_rule()
    {
        var expected = new HashSet<string>(["Example.Contract"], StringComparer.Ordinal);
        var target = new AssemblyContractMetadata("Target", expected, new HashSet<string>(), new HashSet<string>());
        var legacy = new AssemblyContractMetadata("Legacy", new HashSet<string>(), new HashSet<string>(), new HashSet<string>());
        var violations = ContractBoundaryPolicy.EvaluateTypeCompatibility(
            new Dictionary<string, AssemblyContractMetadata> { ["Target"] = target }, legacy, expected);
        Assert.Contains(violations, item => item.Code == "CONTRACT_FORWARDER_MISSING");
    }

    [Fact]
    public void Forbidden_contract_type_is_rejected_for_expected_rule()
    {
        var metadata = new AssemblyContractMetadata(
            "Core",
            new HashSet<string>(),
            new HashSet<string>(["ModelContextProtocol.Server.McpServerToolAttribute"]),
            new HashSet<string>());
        Assert.Contains(ContractBoundaryPolicy.EvaluateForbiddenTypes(metadata), item => item.Code == "CONTRACT_FORBIDDEN_TYPE");
    }


    private static string Fixture(string name) => Path.Combine(
        RepositoryRoot,
        "tests",
        "AgentSession.MCP.ArchitectureTests",
        "Fixtures",
        name);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgentMemoryMCP.slnx")) &&
                Directory.Exists(Path.Combine(directory.FullName, "openspec")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root containing AgentMemoryMCP.slnx and openspec.");
    }

    private static void AssertNoViolations(IEnumerable<ArchitectureViolation> violations)
    {
        var materialized = violations.ToArray();
        Assert.True(materialized.Length == 0, string.Join(Environment.NewLine, materialized.Select(violation =>
            $"{violation.Code}: {violation.Source} -> {violation.Target}: {violation.Message}")));
    }
}
