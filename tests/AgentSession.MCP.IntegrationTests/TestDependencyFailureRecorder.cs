using System.Collections.Concurrent;
using AgentSession.MCP.Core.Observability;
using AgentSession.MCP.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnuObservability.Mcp.Health;

namespace AgentSession.MCP.IntegrationTests;

internal sealed class TestDependencyFailureRecorder : IMcpDependencyFailureRecorder
{
    private readonly ConcurrentQueue<(string Type, string Operation)> _failures = new();

    public IReadOnlyCollection<(string Type, string Operation)> Failures => _failures.ToArray();

    public void Record(string dependencyType, string dependencyOperation) =>
        _failures.Enqueue((dependencyType, dependencyOperation));
}

internal static class TestDependencyOperationTrackerFactory
{
    public static IDependencyOperationTracker Create(IMcpDependencyFailureRecorder failures)
    {
        using var provider = new ServiceCollection()
            .AddSingleton(failures)
            .AddMemoryStorageConfiguration(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(
                        new Dictionary<string, string?> { ["Repository:Id"] = "test-repository" }
                    )
                    .Build()
            )
            .BuildServiceProvider();
        return provider.GetRequiredService<IDependencyOperationTracker>();
    }
}
