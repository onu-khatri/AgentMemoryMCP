using System.Security.Cryptography;
using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Core.Storage;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Infrastructure.DependencyInjection;
using AgentSession.MCP.Services;
using AgentSession.MCP.Session.Contracts.Capabilities;
using AgentSession.MCP.Session.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSession.MCP.IntegrationTests;

public sealed class CanonicalStorageCompatibilityTests : IDisposable
{
    private const string RepositoryId = "foundation-baseline";
    private const string SessionId = "foundation-session";
    private const string MemoryId = "77266ce7a4fe4383aff0d97a311c7e37";

    private readonly string _fixture = Path.Combine(
        FindRepositoryRoot(),
        "openspec",
        "changes",
        "standalone-mcp-modular-solution-foundation",
        "evidence",
        "baselines",
        "canonical-storage",
        "fixture"
    );
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "canonical-storage-compatibility",
        Guid.NewGuid().ToString("N")
    );

    [Theory]
    [InlineData("intent")]
    [InlineData("mutation-0")]
    [InlineData("mutation-1")]
    [InlineData("receipt")]
    [InlineData("complete")]
    public async Task CheckedInFixtureReadsAndRecoversWithoutFormatMigration(string boundary)
    {
        var fixtureBefore = Snapshot(_fixture);
        CopyTree(_fixture, _root);
        var sessionFile = new ManagedFile(
            ManagedArea.Session,
            SessionId,
            ["coordination", "metadata.json"]
        );
        var memoryFile = new ManagedFile(
            ManagedArea.Learning,
            null,
            ["short-term", "active", "2026-10", MemoryId + ".json"]
        );

        string session;
        string memory;
        using (var provider = InfrastructureTestServices.Build(
                   _root,
                   repositoryId: RepositoryId
               ))
        {
            var store = provider.GetRequiredService<ManagedTransactionStore>();
            var loadedSession = await store.ReadAsync(sessionFile);
            var loadedMemory = await store.ReadAsync(memoryFile);
            Assert.NotNull(loadedSession);
            Assert.NotNull(loadedMemory);
            session = loadedSession;
            memory = loadedMemory;
            AssertSchemaAndIdentity(session, "sessionId", SessionId);
            AssertSchemaAndIdentity(memory, "id", MemoryId);

            store.AfterDurableBoundary = current =>
            {
                if (current == boundary)
                    throw new IOException("Injected compatibility interruption");
            };
            await Assert.ThrowsAsync<IOException>(() =>
                store.CommitAsync(
                    "compatibility-" + boundary,
                    [
                        new(sessionFile, session, ContentHash.Compute(session)),
                        new(memoryFile, memory, ContentHash.Compute(memory)),
                    ]
                )
            );
        }

        using (var restarted = InfrastructureTestServices.Build(
                   _root,
                   repositoryId: RepositoryId
               ))
        {
            var store = restarted.GetRequiredService<ManagedTransactionStore>();
            Assert.Equal(session, await store.ReadAsync(sessionFile));
            Assert.Equal(memory, await store.ReadAsync(memoryFile));
            Assert.Equal(0, await store.GetRecoveryBacklogAsync());
        }

        Assert.Equal(
            new[] { "repositories", "sessions" },
            Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(_root, path).Split(Path.DirectorySeparatorChar)[0])
                .Where(part => part != ".locks")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
        );
        Assert.Equal(fixtureBefore, Snapshot(_fixture));
    }

    [Fact]
    public async Task Session_module_reads_captured_state_without_migration_or_provider()
    {
        var fixtureBefore = Snapshot(_fixture);
        CopyTree(_fixture, _root);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SystemStorage:Root"] = _root,
                ["Repository:Id"] = RepositoryId,
            })
            .Build();
        using var provider = new ServiceCollection()
            .AddAgentSessionInfrastructure(configuration)
            .AddAgentSessionModule(configuration)
            .BuildServiceProvider();
        var context = Context(RepositoryId);

        var sessions = await provider
            .GetRequiredService<ISessionLifecycleCapabilities>()
            .ListAsync(new ListSharedSessionsRequest(), context, default);
        var page = await provider
            .GetRequiredService<ISessionResumeCapabilities>()
            .ResumeAsync(new ResumeSessionRequest(SessionId, "reader"), context, default);

        Assert.Contains(SessionId, sessions.SessionIds);
        Assert.Equal(3, page.Sequence);
        Assert.Equal(3, page.AcknowledgedSequence);
        Assert.Contains(page.Entries, entry => entry.Kind == "artifact");
        Assert.Contains(page.Entries, entry => entry.Kind == "task");
        await Assert.ThrowsAsync<ValidationException>(() => provider
            .GetRequiredService<ISessionResumeCapabilities>()
            .ResumeAsync(
                new ResumeSessionRequest(SessionId, "reader"),
                Context("another-repository"),
                default
            ));
        Assert.Equal(fixtureBefore, Snapshot(_fixture));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private static void AssertSchemaAndIdentity(string json, string identityName, string identity)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(identity, document.RootElement.GetProperty(identityName).GetString());
    }

    private static IReadOnlyDictionary<string, string> Snapshot(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(root, path),
                path => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
                StringComparer.Ordinal
            );

    private static void CopyTree(string source, string target)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (
                File.Exists(Path.Combine(directory.FullName, "AgentMemoryMCP.slnx"))
                && Directory.Exists(Path.Combine(directory.FullName, "openspec"))
            )
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static PlatformRequestContext Context(string repositoryId) => new(
        new ClientIdentity("compatibility-tests"),
        null,
        null,
        new HashSet<string>(StringComparer.Ordinal),
        new RepositoryResource(repositoryId),
        "compatibility-test"
    );
}
