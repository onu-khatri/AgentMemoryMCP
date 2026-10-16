using AgentSession.MCP.Core.Errors;

namespace AgentSession.MCP.Core.Context;

public sealed class PlatformRequestContextAccessor : IPlatformRequestContextAccessor
{
    private PlatformRequestContext? current;

    public PlatformRequestContext Current => current ?? throw new PlatformException(
        new PlatformError(
            ErrorCategory.Unauthorized,
            "request_context_unset",
            "A platform request context has not been established."));

    public void Set(PlatformRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (current is not null)
            throw new InvalidOperationException("A platform request context is already established.");

        current = context;
    }

    public void Clear() => current = null;
}
