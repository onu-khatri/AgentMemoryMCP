using OnuObservability.Mcp.Health;

namespace AgentSession.MCP.Observability;

internal sealed class NoOpMcpDependencyFailureRecorder : IMcpDependencyFailureRecorder
{
    public void Record(string dependencyType, string dependencyOperation)
    {
    }
}
