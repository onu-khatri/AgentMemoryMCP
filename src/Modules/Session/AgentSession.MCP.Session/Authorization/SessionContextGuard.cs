using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Helpers;

namespace AgentSession.MCP.Services;

public sealed class SessionContextGuard(RepositoryResource configuredRepository)
{
    public void RequireRepository(PlatformRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(
                context.Repository.RepositoryId,
                configuredRepository.RepositoryId,
                StringComparison.Ordinal))
            throw new ValidationException(
                "Request repository does not match the configured repository.",
                "repository_mismatch"
            );
    }
}
