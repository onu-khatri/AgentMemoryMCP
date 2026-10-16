using AgentSession.MCP.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Host;

public sealed class HostReadinessCheck(IOptions<SystemStorageOptions> storageOptions) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var root = storageOptions.Value.Root;
        var parent = Directory.GetParent(root);
        if (parent is not null && parent.Exists)
            return Task.FromResult(HealthCheckResult.Healthy("Configured storage location is reachable."));

        return Task.FromResult(HealthCheckResult.Unhealthy("Configured storage location is not reachable."));
    }
}
