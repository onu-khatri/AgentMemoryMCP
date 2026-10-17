using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AgentSession.MCP.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics => metrics.AddMeter(serviceName))
            .WithTracing(tracing => tracing.AddSource(serviceName));

        builder.Services.ConfigureHttpClientDefaults(http =>
            http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30)));

        return builder;
    }

    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder) =>
        AddServiceDefaults(builder, builder.Environment.ApplicationName);

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
        app.MapGet("/health/ready", async (HealthCheckService healthChecks, HttpContext context, CancellationToken cancellationToken) =>
        {
            var report = await healthChecks.CheckHealthAsync(check => check.Tags.Contains("ready"), cancellationToken);
            context.Response.StatusCode = report.Entries.Count == 0 || report.Status != HealthStatus.Healthy
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new { status = report.Status.ToString().ToLowerInvariant() }, cancellationToken);
        });
        return app;
    }
}
