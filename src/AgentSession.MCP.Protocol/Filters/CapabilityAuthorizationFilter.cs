using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Core.Errors;
using AgentSession.MCP.Core.Execution;
using AgentSession.MCP.Core.Security;
using AgentSession.MCP.Protocol.Errors;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentSession.MCP.Protocol.Filters;

public static class CapabilityAuthorizationFilter
{
    public static void Require(PlatformRequestContext context, string capability)
    {
        if (!CapabilityCatalog.TryGet(capability, out var descriptor))
            throw new ForbiddenException();

        var requirement = descriptor.Authorization;
        if (requirement.RequiresAuthenticatedClient && string.Equals(context.Client.ClientId, "unknown", StringComparison.Ordinal))
            throw new UnauthorizedException();
        if (requirement.RequiredScopes.Any(scope => !context.Scopes.Contains(scope)) ||
            requirement.ResourceAccess == ResourceAccessRequirement.RepositoryBound && string.IsNullOrWhiteSpace(context.Repository.RepositoryId))
            throw new ForbiddenException();
    }

    internal static bool IsVisible(PlatformRequestContext context, CapabilityDescriptor descriptor)
    {
        try
        {
            Require(context, descriptor.OperationId);
            return true;
        }
        catch (Exception error) when (error is UnauthorizedException or ForbiddenException or PlatformException)
        {
            return false;
        }
    }
}

public interface ICapabilityRateLimiter
{
    ValueTask<bool> TryAcquireAsync(
        PlatformRequestContext context,
        string operationId,
        CancellationToken cancellationToken);
}
