namespace AgentSession.MCP.Core.Context;

public interface IPlatformRequestContextAccessor
{
    PlatformRequestContext Current { get; }

    void Set(PlatformRequestContext context);

    void Clear();
}
