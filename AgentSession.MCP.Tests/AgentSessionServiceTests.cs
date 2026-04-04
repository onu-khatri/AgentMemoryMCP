using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Options;
using AgentSession.MCP.Services;
using AgentSession.MCP.Tools;
using AgentSession.MCP.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Tests;

public sealed class AgentSessionServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAgentSessionService _service;

    public AgentSessionServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "agentsession-mcp-tests", Guid.NewGuid().ToString("N"));
        _loggerFactory = LoggerFactory.Create(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));

        var fileSystem = new SystemFileSystem();
        var serializer = new YamlDotNetSerializer();
        var pathBuilder = new SessionStoragePathBuilder(Microsoft.Extensions.Options.Options.Create(new SessionStorageOptions { RootPath = _tempRoot }));

        IAgentSessionStore store = new FileAgentSessionStore(fileSystem, serializer, pathBuilder, _loggerFactory.CreateLogger<FileAgentSessionStore>());
        _service = new AgentSessionService(store, serializer, pathBuilder, _loggerFactory.CreateLogger<AgentSessionService>());
    }

    [Fact]
    public async Task CreateSession_ThenActivateExisting_Works()
    {
        var created = await _service.CreateOrActivateSessionAsync(new CreateOrActivateSessionRequest
        {
            SessionId = "weekly-planning",
            InitialState = "planning",
            AgentName = "copilot"
        }, CancellationToken.None);

        Assert.Contains("created_or_activated: created", created, StringComparison.Ordinal);

        var activated = await _service.CreateOrActivateSessionAsync(new CreateOrActivateSessionRequest
        {
            SessionId = "weekly-planning",
            AgentName = "copilot"
        }, CancellationToken.None);

        Assert.Contains("created_or_activated: activated", activated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppendMemory_AndReadMemory_ReturnsPersistedText()
    {
        await _service.CreateOrActivateSessionAsync(new CreateOrActivateSessionRequest { SessionId = "memory-session" }, CancellationToken.None);

        await _service.AppendAgentMemoryAsync(new AppendAgentMemoryRequest
        {
            SessionId = "memory-session",
            Content = "Durable decision: use optimistic locking.",
            AgentName = "copilot",
            SectionTitle = "Decisions"
        }, CancellationToken.None);

        var result = await _service.ReadAgentMemoryAsync(new ReadAgentMemoryRequest
        {
            SessionId = "memory-session",
            LatestLogEntries = 5
        }, CancellationToken.None);

        Assert.Contains("Durable decision", result.Memory, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ArtifactCreateReadList_Works()
    {
        await _service.CreateOrActivateSessionAsync(new CreateOrActivateSessionRequest { SessionId = "artifact-session" }, CancellationToken.None);

        var create = await _service.CreateAgentArtifactAsync(new CreateAgentArtifactRequest
        {
            SessionId = "artifact-session",
            ArtifactName = "sprint-plan",
            Description = "Implementation checklist",
            IntendedUse = "Execution",
            Content = "# Sprint Plan\n- task 1",
            Tags = new[] { "plan", "sprint" },
            Overwrite = false
        }, CancellationToken.None);

        Assert.True(create.Success);

        var read = await _service.ReadAgentArtifactAsync(new ReadAgentArtifactRequest
        {
            SessionId = "artifact-session",
            ArtifactName = "sprint-plan"
        }, CancellationToken.None);

        Assert.Equal("sprint-plan", read.Metadata.Name);
        Assert.Contains("Sprint Plan", read.Content, StringComparison.Ordinal);

        var list = await _service.ListAgentArtifactsAsync(new ListAgentArtifactsRequest
        {
            SessionId = "artifact-session"
        }, CancellationToken.None);

        Assert.Single(list.Artifacts);
    }

    [Fact]
    public async Task InvalidNames_AreRejected()
    {
        await Assert.ThrowsAsync<ValidationException>(async () =>
            await _service.CreateOrActivateSessionAsync(new CreateOrActivateSessionRequest { SessionId = "////" }, CancellationToken.None));
    }

    [Fact]
    public async Task MissingSession_ReadMemoryThrows()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
            await _service.ReadAgentMemoryAsync(new ReadAgentMemoryRequest { SessionId = "missing-session" }, CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentMemoryAppends_DoNotCorruptContent()
    {
        await _service.CreateOrActivateSessionAsync(new CreateOrActivateSessionRequest { SessionId = "concurrency-session" }, CancellationToken.None);

        var tasks = Enumerable.Range(1, 25)
            .Select(i => _service.AppendAgentMemoryAsync(new AppendAgentMemoryRequest
            {
                SessionId = "concurrency-session",
                Content = $"Concurrent entry {i}",
                AgentName = "copilot",
                SectionTitle = "Concurrent"
            }, CancellationToken.None));

        await Task.WhenAll(tasks);

        var result = await _service.ReadAgentMemoryAsync(new ReadAgentMemoryRequest { SessionId = "concurrency-session" }, CancellationToken.None);
        Assert.Contains("Concurrent entry 1", result.Memory, StringComparison.Ordinal);
        Assert.Contains("Concurrent entry 25", result.Memory, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
