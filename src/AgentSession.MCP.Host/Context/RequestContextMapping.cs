using System.Diagnostics;
using System.Security.Claims;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Core.Errors;
using AgentSession.MCP.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
namespace AgentSession.MCP.Host.Context;

public static class RequestContextMapping
{
    public static PlatformRequestContext Map(HttpContext httpContext, RepositoryOptions repositoryOptions)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(repositoryOptions);

        var requestedRepository = httpContext.Request.Headers["X-Repository-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(requestedRepository) && !string.Equals(requestedRepository, repositoryOptions.Id, StringComparison.Ordinal))
            throw new PlatformException(new PlatformError(
                ErrorCategory.Forbidden,
                "repository_mismatch",
                "The requested repository is not available."));

        var principal = httpContext.User;
        var clientId = principal.FindFirstValue("client_id") ?? "anonymous";
        var scopes = principal.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
        var correlationId = httpContext.TraceIdentifier;
        if (Activity.Current?.TraceId is { } traceId && traceId != default)
            correlationId = traceId.ToString();
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var header)
            && PlatformRequestContext.IsValidCorrelationId(header.ToString()))
            correlationId = header.ToString();

        return new PlatformRequestContext(
            new ClientIdentity(clientId),
            principal.FindFirstValue("tenant_id"),
            principal.FindFirstValue("sub"),
            scopes,
            new RepositoryResource(repositoryOptions.Id),
            correlationId);
    }

    public static IDisposable Establish(HttpContext httpContext, IPlatformRequestContextAccessor accessor, IOptions<RepositoryOptions> repositoryOptions)
    {
        accessor.Set(Map(httpContext, repositoryOptions.Value));
        return new ContextScope(accessor);
    }

    private sealed class ContextScope(IPlatformRequestContextAccessor accessor) : IDisposable
    {
        public void Dispose() => accessor.Clear();
    }
}
