using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Tools;

namespace AgentSession.MCP.Tests;

public sealed class AgentSessionToolsTests
{
    [Fact]
    public async Task ListSessions_ForwardsToService()
    {
        var expected = new ListAgentSessionsItem { SessionId = "s1" };
        var service = new StubAgentSessionService
        {
            ListSessions = _ => Task.FromResult<IReadOnlyCollection<ListAgentSessionsItem>>([expected])
        };

        var tools = new AgentSessionTools(service);
        var result = await tools.list_agent_sessions();

        Assert.Single(result);
        Assert.Equal("s1", result.First().SessionId);
    }

    [Fact]
    public async Task CreateOrActivate_ValidationException_IsWrapped()
    {
        var service = new StubAgentSessionService
        {
            CreateOrActivate = (_, _) => throw new ValidationException("bad")
        };

        var tools = new AgentSessionTools(service);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tools.create_or_activate_session(new CreateOrActivateSessionRequest()));
        Assert.Contains("Invalid request", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAgentMemory_ValidationException_IsWrapped()
    {
        var service = new StubAgentSessionService
        {
            ReadMemory = (_, _) => throw new ValidationException("bad")
        };

        var tools = new AgentSessionTools(service);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.read_agent_memory(new ReadAgentMemoryRequest()));
    }

    [Fact]
    public async Task AppendAgentMemory_ValidationException_IsWrapped()
    {
        var service = new StubAgentSessionService
        {
            AppendMemory = (_, _) => throw new ValidationException("bad")
        };

        var tools = new AgentSessionTools(service);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.append_agent_memory(new AppendAgentMemoryRequest()));
    }

    [Fact]
    public async Task LogAgentEvent_ValidationException_IsWrapped()
    {
        var service = new StubAgentSessionService
        {
            LogEvent = (_, _) => throw new ValidationException("bad")
        };

        var tools = new AgentSessionTools(service);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.log_agent_event(new LogAgentEventRequest()));
    }

    [Fact]
    public async Task CreateArtifact_ValidationException_IsWrapped()
    {
        var service = new StubAgentSessionService
        {
            CreateArtifact = (_, _) => throw new ValidationException("bad")
        };

        var tools = new AgentSessionTools(service);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.create_agent_artifact(new CreateAgentArtifactRequest()));
    }

    [Fact]
    public async Task ReadArtifact_ValidationException_IsWrapped()
    {
        var service = new StubAgentSessionService
        {
            ReadArtifact = (_, _) => throw new ValidationException("bad")
        };

        var tools = new AgentSessionTools(service);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.read_agent_artifact(new ReadAgentArtifactRequest()));
    }

    [Fact]
    public async Task ListArtifacts_ValidationException_IsWrapped()
    {
        var service = new StubAgentSessionService
        {
            ListArtifacts = (_, _) => throw new ValidationException("bad")
        };

        var tools = new AgentSessionTools(service);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.list_agent_artifacts(new ListAgentArtifactsRequest()));
    }

    [Fact]
    public async Task SaveFinalPlan_ValidationException_IsWrapped()
    {
        var service = new StubAgentSessionService
        {
            SaveFinalPlan = (_, _) => throw new ValidationException("bad")
        };

        var tools = new AgentSessionTools(service);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.save_final_plan(new SaveFinalPlanRequest()));
    }

    [Fact]
    public async Task GetLatestFinalPlan_ValidationException_IsWrapped()
    {
        var service = new StubAgentSessionService
        {
            GetLatestFinalPlan = (_, _) => throw new ValidationException("bad")
        };

        var tools = new AgentSessionTools(service);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.get_latest_final_plan(new GetLatestFinalPlanRequest()));
    }

    private sealed class StubAgentSessionService : IAgentSessionService
    {
        public Func<CancellationToken, Task<IReadOnlyCollection<ListAgentSessionsItem>>>? ListSessions { get; set; }
        public Func<CreateOrActivateSessionRequest, CancellationToken, Task<string>>? CreateOrActivate { get; set; }
        public Func<ReadAgentMemoryRequest, CancellationToken, Task<ReadAgentMemoryResponse>>? ReadMemory { get; set; }
        public Func<AppendAgentMemoryRequest, CancellationToken, Task<AppendAgentMemoryResponse>>? AppendMemory { get; set; }
        public Func<LogAgentEventRequest, CancellationToken, Task<OperationResult>>? LogEvent { get; set; }
        public Func<CreateAgentArtifactRequest, CancellationToken, Task<CreateAgentArtifactResponse>>? CreateArtifact { get; set; }
        public Func<ReadAgentArtifactRequest, CancellationToken, Task<ReadAgentArtifactResponse>>? ReadArtifact { get; set; }
        public Func<ListAgentArtifactsRequest, CancellationToken, Task<ListAgentArtifactsResponse>>? ListArtifacts { get; set; }
        public Func<SaveFinalPlanRequest, CancellationToken, Task<SaveFinalPlanResponse>>? SaveFinalPlan { get; set; }
        public Func<GetLatestFinalPlanRequest, CancellationToken, Task<GetLatestFinalPlanResponse>>? GetLatestFinalPlan { get; set; }

        public Task<IReadOnlyCollection<ListAgentSessionsItem>> ListAgentSessionsAsync(CancellationToken cancellationToken)
            => ListSessions?.Invoke(cancellationToken) ?? Task.FromResult<IReadOnlyCollection<ListAgentSessionsItem>>([]);

        public Task<string> CreateOrActivateSessionAsync(CreateOrActivateSessionRequest request, CancellationToken cancellationToken)
            => CreateOrActivate?.Invoke(request, cancellationToken) ?? Task.FromResult("ok");

        public Task<ReadAgentMemoryResponse> ReadAgentMemoryAsync(ReadAgentMemoryRequest request, CancellationToken cancellationToken)
            => ReadMemory?.Invoke(request, cancellationToken) ?? Task.FromResult(new ReadAgentMemoryResponse());

        public Task<AppendAgentMemoryResponse> AppendAgentMemoryAsync(AppendAgentMemoryRequest request, CancellationToken cancellationToken)
            => AppendMemory?.Invoke(request, cancellationToken) ?? Task.FromResult(new AppendAgentMemoryResponse { Success = true });

        public Task<OperationResult> LogAgentEventAsync(LogAgentEventRequest request, CancellationToken cancellationToken)
            => LogEvent?.Invoke(request, cancellationToken) ?? Task.FromResult(new OperationResult { Success = true });

        public Task<CreateAgentArtifactResponse> CreateAgentArtifactAsync(CreateAgentArtifactRequest request, CancellationToken cancellationToken)
            => CreateArtifact?.Invoke(request, cancellationToken) ?? Task.FromResult(new CreateAgentArtifactResponse { Success = true });

        public Task<ReadAgentArtifactResponse> ReadAgentArtifactAsync(ReadAgentArtifactRequest request, CancellationToken cancellationToken)
            => ReadArtifact?.Invoke(request, cancellationToken) ?? Task.FromResult(new ReadAgentArtifactResponse());

        public Task<ListAgentArtifactsResponse> ListAgentArtifactsAsync(ListAgentArtifactsRequest request, CancellationToken cancellationToken)
            => ListArtifacts?.Invoke(request, cancellationToken) ?? Task.FromResult(new ListAgentArtifactsResponse());

        public Task<SaveFinalPlanResponse> SaveFinalPlanAsync(SaveFinalPlanRequest request, CancellationToken cancellationToken)
            => SaveFinalPlan?.Invoke(request, cancellationToken) ?? Task.FromResult(new SaveFinalPlanResponse { Success = true });

        public Task<GetLatestFinalPlanResponse> GetLatestFinalPlanAsync(GetLatestFinalPlanRequest request, CancellationToken cancellationToken)
            => GetLatestFinalPlan?.Invoke(request, cancellationToken) ?? Task.FromResult(new GetLatestFinalPlanResponse { Success = true, SessionId = request.SessionId });
    }
}
