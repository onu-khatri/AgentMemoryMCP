using System.Threading.RateLimiting;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Protocol.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PlatformHostOptions = AgentSession.MCP.Host.Configuration.HostOptions;

namespace AgentSession.MCP.Host.RateLimiting;

public sealed class HostRateLimiter(
    IOptions<PlatformHostOptions> options,
    IHttpContextAccessor httpContextAccessor) : ICapabilityRateLimiter, IDisposable
{
    public const string RateLimitRejectedItem = "AgentSession.MCP.RateLimitRejected";
    public const string RetryAfterItem = "AgentSession.MCP.RateLimitRetryAfter";
    public const string OperationItem = "AgentSession.MCP.RateLimitOperation";
    private readonly PartitionedRateLimiter<string> limiter = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.Value.RateLimitPermitLimit,
                QueueLimit = options.Value.RateLimitQueueLimit,
                Window = TimeSpan.FromSeconds(options.Value.RateLimitWindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));

    public ValueTask<bool> TryAcquireAsync(
        PlatformRequestContext context,
        string operationId,
        CancellationToken cancellationToken)
    {
        return AcquireAsync(context, operationId, cancellationToken);
    }

    public static string ResolvePartitionKey(PlatformRequestContext context) =>
        string.Join('|', Normalize(context.Client.ClientId), Normalize(context.TenantId ?? "none"));

    private async ValueTask<bool> AcquireAsync(PlatformRequestContext context, string operationId, CancellationToken cancellationToken)
    {
        using var lease = await limiter.AcquireAsync(ResolvePartitionKey(context), 1, cancellationToken);
        if (lease.IsAcquired)
            return true;

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            httpContext.Items[RateLimitRejectedItem] = true;
            httpContext.Items[OperationItem] = operationId;
            var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan delay)
                ? Math.Clamp((int)Math.Ceiling(delay.TotalSeconds), 1, 60)
                : Math.Min(options.Value.RateLimitWindowSeconds, 60);
            httpContext.Items[RetryAfterItem] = retryAfter;
        }
        return false;
    }

    public void Dispose() => limiter.Dispose();

    private static string Normalize(string value)
    {
        var normalized = new string(value.Trim().ToLowerInvariant().Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-').ToArray());
        if (normalized.Length == 0)
            return "unknown";
        return normalized.Length <= 64 ? normalized : normalized[..64];
    }
}
