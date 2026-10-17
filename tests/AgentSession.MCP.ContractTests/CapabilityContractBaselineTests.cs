using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using AgentSession.MCP.Protocol.Surfaces.AgentMemory;
using AgentSession.MCP.Protocol.Surfaces.Session;
using AgentSession.MCP.Protocol.Mapping;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.AgentMemory.Contracts.Registration;
using AgentSession.MCP.Core.Execution;
using AgentSession.MCP.Session.Contracts.Registration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace AgentSession.MCP.ContractTests;

public sealed class CapabilityContractBaselineTests
{
    private static readonly string[] ExpectedNames =
    [
        "append_agent_memory", "checkpoint_agent_session", "coordinate_agent_task", "create_or_activate_session", "list_agent_sessions", "resume_agent_session",
        "memory_archive", "memory_cleanup", "memory_commit_compaction", "memory_compact", "memory_delete", "memory_get", "memory_list_review", "memory_mark_reviewed",
        "memory_prepare_compaction", "memory_promote", "memory_recall", "memory_rebuild_indexes", "memory_record_event", "memory_record_outcome", "memory_reindex",
        "memory_remember", "memory_retire", "memory_status", "memory_supersede", "memory_update"
    ];

    [Fact]
    public void Protocol_exposes_the_authoritative_26_tool_manifest()
    {
        var tools = typeof(SessionTools).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Concat(typeof(AgentMemoryTools).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedNames.OrderBy(name => name, StringComparer.Ordinal), tools);
        Assert.Equal(26, tools.Length);
        Assert.Equal(26, tools.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(FindRoot(), "src", "AgentSession.MCP.Protocol"), "*Resource*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(FindRoot(), "src", "AgentSession.MCP.Protocol"), "*Prompt*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Every_adapter_has_a_description_and_protocol_attribute()
    {
        var methods = typeof(SessionTools).GetMethods().Concat(typeof(AgentMemoryTools).GetMethods())
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null);
        Assert.All(methods, method => Assert.False(string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description)));
    }

    [Fact]
    public void Capability_descriptors_use_the_approved_operation_scopes()
    {
        var descriptors = SessionModuleRegistrationDescriptor.Value.Capabilities
            .Concat(AgentMemoryModuleRegistrationDescriptor.Value.Capabilities)
            .ToArray();

        Assert.Equal(26, descriptors.Length);
        Assert.All(descriptors, descriptor =>
        {
            var module = descriptor.OperationId.StartsWith("memory_", StringComparison.Ordinal)
                ? "agent-memory"
                : "session";
            var expected = $"mcp:{module}:{descriptor.Access.ToString().ToLowerInvariant()}";
            Assert.Equal([expected], descriptor.Authorization.RequiredScopes);
        });
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("renamed")]
    [InlineData("new")]
    public void Manifest_deltas_are_rejected(string delta)
    {
        var actual = ExpectedNames.ToList();
        switch (delta)
        {
            case "duplicate": actual.Add(ExpectedNames[0]); break;
            case "missing": actual.RemoveAt(0); break;
            case "renamed": actual[0] = "renamed_capability"; break;
            case "new": actual.Add("new_capability"); break;
        }
        Assert.False(ManifestMatches(actual, ExpectedNames));
    }

    [Fact]
    public async Task Unauthorized_explicit_adapter_pipeline_does_not_call_the_contract()
    {
        var called = false;
        var pipeline = new ProtocolAdapterPipeline(new TestContextAccessor(new(
            new ClientIdentity("unknown"), null, null, new HashSet<string>(), new RepositoryResource("repo"), "corr")));

        await Assert.ThrowsAsync<McpException>(() => pipeline.ExecuteAsync(
            "memory_status",
            new object(),
            (_, _) =>
            {
                called = true;
                return Task.FromResult(new object());
            },
            CancellationToken.None));

        Assert.False(called);
    }

    [Fact]
    public async Task Protocol_bounds_reject_oversized_input_before_the_contract()
    {
        var called = false;
        var context = new PlatformRequestContext(
            new ClientIdentity("client"), null, null, new HashSet<string> { "memory" }, new RepositoryResource("repo"), "corr");
        var pipeline = new ProtocolAdapterPipeline(new TestContextAccessor(context));

        await Assert.ThrowsAsync<McpException>(() => pipeline.ExecuteAsync(
            "memory_remember",
            new { content = new string('x', 65_537) },
            (_, _) =>
            {
                called = true;
                return Task.FromResult(new object());
            },
            CancellationToken.None));

        Assert.False(called);
    }

    private static bool ManifestMatches(IEnumerable<string> actual, IEnumerable<string> expected) =>
        actual.Count() == expected.Count() && actual.Distinct(StringComparer.Ordinal).OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x), StringComparer.Ordinal);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgentMemoryMCP.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class TestContextAccessor(PlatformRequestContext context) : IPlatformRequestContextAccessor
    {
        public PlatformRequestContext Current { get; private set; } = context;
        public void Set(PlatformRequestContext value) => Current = value;
        public void Clear() { }
    }
}
