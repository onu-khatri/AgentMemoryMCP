using System.Collections.Concurrent;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using PlatformHostOptions = AgentSession.MCP.Host.Configuration.HostOptions;

namespace AgentSession.MCP.Host.Security;

public sealed class PreAuthenticationRateLimiter(IOptions<PlatformHostOptions> options) : IDisposable
{
    public const string RejectedItem = "AgentSession.MCP.PreAuthenticationRateLimitRejected";
    public const string RetryAfterItem = "AgentSession.MCP.PreAuthenticationRetryAfter";

    private readonly PartitionedRateLimiter<string> limiter = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.Value.PreAuthenticationPermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(options.Value.PreAuthenticationWindowSeconds),
                AutoReplenishment = true
            }));

    public async ValueTask<bool> TryAcquireAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var key = ResolveKey(context);
        using var lease = await limiter.AcquireAsync(key, 1, cancellationToken);
        if (lease.IsAcquired)
            return true;

        var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan delay)
            ? Math.Clamp((int)Math.Ceiling(delay.TotalSeconds), 1, 60)
            : Math.Min(options.Value.PreAuthenticationWindowSeconds, 60);
        context.Items[RejectedItem] = true;
        context.Items[RetryAfterItem] = retryAfter;
        return false;
    }

    internal static string ResolveKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        var value = address is null ? "unknown" : address.MapToIPv4().ToString();
        return $"bearer:{value}";
    }

    public void Dispose() => limiter.Dispose();
}
