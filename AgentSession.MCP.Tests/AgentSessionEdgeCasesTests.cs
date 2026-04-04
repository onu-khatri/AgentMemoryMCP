using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Models;
using AgentSession.MCP.Options;
using AgentSession.MCP.Services;
using AgentSession.MCP.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Tests;

public sealed class AgentSessionEdgeCasesTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly ILoggerFactory _loggerFactory;
    private readonly FileAgentSessionStore _store;
    private readonly AgentSessionService _service;

    public AgentSessionEdgeCasesTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "agentsession-edge-tests", Guid.NewGuid().ToString("N"));
        _loggerFactory = LoggerFactory.Create(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));

        var fileSystem = new SystemFileSystem();
        var serializer = new YamlDotNetSerializer();
        var pathBuilder = new SessionStoragePathBuilder(Microsoft.Extensions.Options.Options.Create(new SessionStorageOptions { RootPath = _tempRoot }));

        _store = new FileAgentSessionStore(fileSystem, serializer, pathBuilder, _loggerFactory.CreateLogger<FileAgentSessionStore>());
        _service = new AgentSessionService(_store, serializer, pathBuilder, _loggerFactory.CreateLogger<AgentSessionService>());
    }

    [Fact]
    public async Task CreateOrActivate_WithNullSessionId_GeneratesReadableId()
    {
        var result = await _store.CreateOrActivateSessionAsync(null, "init", "agent", CancellationToken.None);

        Assert.True(result.Created);
        Assert.StartsWith("session-", result.Session.SessionId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadMemory_WhenMemoryFileMissing_ReturnsEmptyString()
    {
        var created = await _store.CreateOrActivateSessionAsync("memory-missing", null, null, CancellationToken.None);
        File.Delete(created.Paths.MemoryFilePath);

        var memory = await _store.ReadMemoryAsync("memory-missing", CancellationToken.None);
        Assert.Equal(string.Empty, memory);
    }

    [Fact]
    public async Task CreateArtifact_WithoutOverwrite_WhenExists_Throws()
    {
        await _store.CreateOrActivateSessionAsync("artifact-overwrite", null, null, CancellationToken.None);

        await _store.CreateOrUpdateArtifactAsync("artifact-overwrite", "doc1", "desc", "use", "body", null, null, overwrite: false, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _store.CreateOrUpdateArtifactAsync("artifact-overwrite", "doc1", "desc", "use", "new", null, null, overwrite: false, CancellationToken.None));
    }

    [Fact]
    public async Task CreateArtifact_WithOverwrite_UpdatesContent()
    {
        await _store.CreateOrActivateSessionAsync("artifact-update", null, null, CancellationToken.None);

        await _store.CreateOrUpdateArtifactAsync("artifact-update", "doc2", "desc", "use", "v1", null, null, overwrite: false, CancellationToken.None);
        await _store.CreateOrUpdateArtifactAsync("artifact-update", "doc2", "desc", "use", "v2", null, null, overwrite: true, CancellationToken.None);

        var read = await _store.ReadArtifactAsync("artifact-update", "doc2", CancellationToken.None);
        Assert.Equal("v2", read.Content);
    }

    [Fact]
    public async Task ListArtifacts_SkipsMalformedArtifactFiles()
    {
        var created = await _store.CreateOrActivateSessionAsync("bad-artifacts", null, null, CancellationToken.None);
        Directory.CreateDirectory(created.Paths.ArtifactDirectoryPath);
        await File.WriteAllTextAsync(Path.Combine(created.Paths.ArtifactDirectoryPath, "broken.md"), "not front matter", CancellationToken.None);

        var list = await _store.ListArtifactsAsync("bad-artifacts", CancellationToken.None);
        Assert.Empty(list);
    }

    [Fact]
    public async Task ReadLogs_WithTakeLastZero_ReturnsAllLogs()
    {
        await _store.CreateOrActivateSessionAsync("logs-all", null, null, CancellationToken.None);

        await _store.AppendLogAsync("logs-all", new AgentSessionLogEntry { Timestamp = DateTimeOffset.UtcNow, Message = "m1", AgentName = "a", Level = "Info" }, CancellationToken.None);
        await _store.AppendLogAsync("logs-all", new AgentSessionLogEntry { Timestamp = DateTimeOffset.UtcNow, Message = "m2", AgentName = "a", Level = "Info" }, CancellationToken.None);

        var logs = await _store.ReadLogsAsync("logs-all", 0, CancellationToken.None);
        Assert.Equal(2, logs.Count);
    }

    [Fact]
    public async Task GetSessionState_WhenStateMissing_Throws()
    {
        var created = await _store.CreateOrActivateSessionAsync("missing-state", null, null, CancellationToken.None);
        File.Delete(created.Paths.StateFilePath);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.GetSessionStateAsync("missing-state", CancellationToken.None));
    }

    [Fact]
    public async Task ServiceValidationPaths_ThrowExpectedExceptions()
    {
        await _store.CreateOrActivateSessionAsync("svc-validation", null, null, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() => _service.AppendAgentMemoryAsync(new AppendAgentMemoryRequest
        {
            SessionId = "svc-validation",
            Content = ""
        }, CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(() => _service.LogAgentEventAsync(new LogAgentEventRequest
        {
            SessionId = "svc-validation",
            Message = "",
            AgentName = "a",
            Level = "Info"
        }, CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(() => _service.LogAgentEventAsync(new LogAgentEventRequest
        {
            SessionId = "svc-validation",
            Message = "m",
            AgentName = "",
            Level = "Info"
        }, CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(() => _service.LogAgentEventAsync(new LogAgentEventRequest
        {
            SessionId = "svc-validation",
            Message = "m",
            AgentName = "a",
            Level = "Fatal"
        }, CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAgentArtifactAsync(new CreateAgentArtifactRequest
        {
            SessionId = "svc-validation",
            ArtifactName = "",
            Description = "d",
            IntendedUse = "u"
        }, CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAgentArtifactAsync(new CreateAgentArtifactRequest
        {
            SessionId = "svc-validation",
            ArtifactName = "a1",
            Description = "",
            IntendedUse = "u"
        }, CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAgentArtifactAsync(new CreateAgentArtifactRequest
        {
            SessionId = "svc-validation",
            ArtifactName = "a1",
            Description = "d",
            IntendedUse = ""
        }, CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(() => _service.ReadAgentArtifactAsync(new ReadAgentArtifactRequest
        {
            SessionId = "svc-validation",
            ArtifactName = ""
        }, CancellationToken.None));
    }

    [Fact]
    public async Task ServiceLogEvent_BlankLevel_DefaultsToInfo()
    {
        await _store.CreateOrActivateSessionAsync("svc-level", null, null, CancellationToken.None);

        var result = await _service.LogAgentEventAsync(new LogAgentEventRequest
        {
            SessionId = "svc-level",
            Message = "log",
            AgentName = "agent",
            Level = " "
        }, CancellationToken.None);

        Assert.True(result.Success);

        var logs = await _store.ReadLogsAsync("svc-level", 1, CancellationToken.None);
        Assert.Equal("Info", logs.Single().Level);
    }

    [Fact]
    public async Task StoreGetLatestFinalPlan_WhenNone_ReturnsNull()
    {
        await _store.CreateOrActivateSessionAsync("store-no-plan", null, null, CancellationToken.None);

        var latest = await _store.GetLatestFinalPlanAsync("store-no-plan", CancellationToken.None);
        Assert.Null(latest);
    }

    [Fact]
    public async Task StoreSaveFinalPlan_UsesUtcTimestampedFilenamePattern()
    {
        await _store.CreateOrActivateSessionAsync("store-plan-pattern", null, null, CancellationToken.None);

        var saved = await _store.SaveFinalPlanAsync(
            "store-plan-pattern",
            "# Stored Plan",
            "Stored",
            "copilot",
            null,
            CancellationToken.None);

        Assert.Matches("^final-plan-\\d{8}-\\d{6}-\\d{3}(?:-[a-z0-9]{6})?\\.md$", saved.Metadata.FileName);
        Assert.Equal(saved.Metadata.CreatedAt, saved.Metadata.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, saved.Metadata.CreatedAt.Offset);
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
