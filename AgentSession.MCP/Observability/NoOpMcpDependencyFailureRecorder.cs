using OnuObservability.Mcp;

namespace AgentSession.MCP.Observability;

internal sealed class NoOpMcpDependencyFailureRecorder : IMcpDependencyFailureRecorder
{
    public void Record(string dependencyType, string dependencyOperation)
    {
    }
}
