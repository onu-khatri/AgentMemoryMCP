using System.Collections.Concurrent;
using OnuObservability.Mcp;

namespace AgentSession.MCP.Tests;

internal sealed class TestDependencyFailureRecorder : IMcpDependencyFailureRecorder
{
    private readonly ConcurrentQueue<(string Type, string Operation)> _failures = new();

    public IReadOnlyCollection<(string Type, string Operation)> Failures => _failures.ToArray();

    public void Record(string dependencyType, string dependencyOperation) =>
        _failures.Enqueue((dependencyType, dependencyOperation));
}
