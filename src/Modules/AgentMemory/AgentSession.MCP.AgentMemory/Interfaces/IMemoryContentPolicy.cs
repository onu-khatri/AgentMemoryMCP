using AgentSession.MCP.Core.Storage;
namespace AgentSession.MCP.Interfaces;

public interface IMemoryContentPolicy
{
    void Validate<T>(T value);
}
