using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Options;
using AgentSession.MCP.Services;
using AgentSession.MCP.Tools;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSession.MCP.Tests;

public sealed class AgentSessionServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ServiceProvider _serviceProvider;
    private readonly IAgentSessionService _service;

    public AgentSessionServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "agentsession-mcp-tests", Guid.NewGuid().ToString("N"));
        _loggerFactory = LoggerFactory.Create(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));
        services.AddAgentSessionServer();
        services.Configure<SessionStorageOptions>(options => options.RootPath = _tempRoot);

        _serviceProvider = services.BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<IAgentSessionService>();
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

    [Fact]
    public async Task SaveFinalPlan_Twice_CreatesDistinctFiles_AndLatestIsNewest()
    {
        await _service.CreateOrActivateSessionAsync(new CreateOrActivateSessionRequest { SessionId = "plan-session" }, CancellationToken.None);

        var first = await _service.SaveFinalPlanAsync(new SaveFinalPlanRequest
        {
            SessionId = "plan-session",
            PlanContent = "# Plan A",
            PlanTitle = "Plan A",
            AgentName = "copilot"
        }, CancellationToken.None);

        await Task.Delay(5);

        var second = await _service.SaveFinalPlanAsync(new SaveFinalPlanRequest
        {
            SessionId = "plan-session",
            PlanContent = "# Plan B",
            PlanTitle = "Plan B",
            AgentName = "copilot"
        }, CancellationToken.None);

        Assert.NotEqual(first.FileName, second.FileName);

        var artifacts = await _service.ListAgentArtifactsAsync(new ListAgentArtifactsRequest { SessionId = "plan-session" }, CancellationToken.None);
        var finalPlans = artifacts.Artifacts.Where(a => a.FileName.StartsWith("final-plan-", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, finalPlans.Count);

        var latest = await _service.GetLatestFinalPlanAsync(new GetLatestFinalPlanRequest { SessionId = "plan-session" }, CancellationToken.None);
        Assert.True(latest.Success);
        Assert.False(latest.NotFound);
        Assert.NotNull(latest.Metadata);
        Assert.Equal(second.FileName, latest.Metadata!.FileName);
        Assert.Equal("# Plan B", latest.PlanContent);
    }

    [Fact]
    public async Task GetLatestFinalPlan_WhenNone_ReturnsNotFoundResponse()
    {
        await _service.CreateOrActivateSessionAsync(new CreateOrActivateSessionRequest { SessionId = "no-plan-session" }, CancellationToken.None);

        var response = await _service.GetLatestFinalPlanAsync(new GetLatestFinalPlanRequest
        {
            SessionId = "no-plan-session"
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.True(response.NotFound);
        Assert.Contains("No final plan exists", response.Message, StringComparison.Ordinal);
        Assert.Null(response.Metadata);
        Assert.Null(response.PlanContent);
    }

    [Fact]
    public async Task SaveFinalPlan_InvalidSessionId_IsRejected()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SaveFinalPlanAsync(new SaveFinalPlanRequest
            {
                SessionId = "////",
                PlanContent = "content"
            }, CancellationToken.None));
    }

    [Fact]
    public async Task SaveFinalPlan_ConcurrentSaves_AllPersisted()
    {
        await _service.CreateOrActivateSessionAsync(new CreateOrActivateSessionRequest { SessionId = "concurrent-plan-session" }, CancellationToken.None);

        var tasks = Enumerable.Range(1, 30)
            .Select(i => _service.SaveFinalPlanAsync(new SaveFinalPlanRequest
            {
                SessionId = "concurrent-plan-session",
                PlanContent = $"# Plan {i}",
                PlanTitle = $"Plan {i}",
                AgentName = "copilot"
            }, CancellationToken.None));

        var saved = await Task.WhenAll(tasks);
        Assert.Equal(30, saved.Length);
        Assert.Equal(30, saved.Select(item => item.FileName).Distinct(StringComparer.Ordinal).Count());

        var artifacts = await _service.ListAgentArtifactsAsync(new ListAgentArtifactsRequest { SessionId = "concurrent-plan-session" }, CancellationToken.None);
        var finalPlans = artifacts.Artifacts.Where(a => a.FileName.StartsWith("final-plan-", StringComparison.Ordinal)).ToList();
        Assert.Equal(30, finalPlans.Count);

        var latest = await _service.GetLatestFinalPlanAsync(new GetLatestFinalPlanRequest { SessionId = "concurrent-plan-session" }, CancellationToken.None);
        Assert.True(latest.Success);
        Assert.False(latest.NotFound);
        Assert.NotNull(latest.Metadata);
        Assert.StartsWith("final-plan-", latest.Metadata!.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveFinalPlan_WithPlanningDetails_RoundTripsOnLatestResponse()
    {
        await _service.CreateOrActivateSessionAsync(new CreateOrActivateSessionRequest { SessionId = "detailed-plan-session" }, CancellationToken.None);

        var approvalTime = DateTimeOffset.UtcNow;
        await _service.SaveFinalPlanAsync(new SaveFinalPlanRequest
        {
            SessionId = "detailed-plan-session",
            PlanContent = "# Detailed Plan",
            Constraints = new PlanConstraintsItem
            {
                ProjectKnowledge = ["existing artifact pipeline"],
                ArchitecturalConstraints = ["reuse store abstraction"]
            },
            Assumptions = ["no schema migration required"],
            ImpactAnalysis = new PlanImpactAnalysisItem
            {
                AffectedModuleComponents = ["Tools", "Services", "Store"],
                CrossCuttingConcerns = ["validation", "serialization"],
                PotentialSideEffects = ["larger front matter payload"]
            },
            ImplementationStrategy = new PlanImplementationStrategyItem
            {
                Summary = "Additive extension",
                Approach = ["extend contracts", "reuse metadata"]
            },
            StepByStepPlan = new PlanStepByStepItem
            {
                Notes = ["keep backward compatibility"],
                Steps =
                [
                    new PlanStepItem { Title = "Update contracts", Note = "add optional fields" },
                    new PlanStepItem { Title = "Wire service/store", Note = "map new details" }
                ]
            },
            TechnicalRisks =
            [
                new PlanRiskItem { Risk = "metadata bloat", Mitigation = "optional fields only" }
            ],
            OpenQuestions = ["Should approval be mandatory?"],
            RequiredButSkippedDecisions = ["Retention policy"],
            Approval = new PlanApprovalItem
            {
                IsApproved = true,
                ApprovalTimestamp = approvalTime
            }
        }, CancellationToken.None);

        var latest = await _service.GetLatestFinalPlanAsync(new GetLatestFinalPlanRequest
        {
            SessionId = "detailed-plan-session"
        }, CancellationToken.None);

        Assert.True(latest.Success);
        Assert.NotNull(latest.PlanDetails);
        Assert.Equal("existing artifact pipeline", latest.PlanDetails!.Constraints!.ProjectKnowledge.Single());
        Assert.Equal("reuse store abstraction", latest.PlanDetails.Constraints.ArchitecturalConstraints.Single());
        Assert.Equal("Tools", latest.PlanDetails.ImpactAnalysis!.AffectedModuleComponents.First());
        Assert.Equal("Additive extension", latest.PlanDetails.ImplementationStrategy!.Summary);
        Assert.Equal(2, latest.PlanDetails.StepByStepPlan!.Steps.Count);
        Assert.Equal("metadata bloat", latest.PlanDetails.TechnicalRisks.Single().Risk);
        Assert.Equal("Should approval be mandatory?", latest.PlanDetails.OpenQuestions.Single());
        Assert.Equal("Retention policy", latest.PlanDetails.RequiredButSkippedDecisions.Single());
        Assert.True(latest.PlanDetails.Approval!.IsApproved);
        Assert.Equal(approvalTime.UtcDateTime, latest.PlanDetails.Approval.ApprovalTimestamp!.Value.UtcDateTime);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _loggerFactory.Dispose();

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
