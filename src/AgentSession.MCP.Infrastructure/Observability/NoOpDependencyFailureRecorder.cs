using OnuObservability.Mcp.Health;

namespace AgentSession.MCP.Infrastructure.Observability;

internal sealed class NoOpDependencyFailureRecorder : IMcpDependencyFailureRecorder
{
    public void Record(string dependencyType, string dependencyOperation) { }
}
