using AgentSession.MCP.Options;
using AgentSession.MCP.Services;
using AgentSession.MCP.Session.Contracts.Capabilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSession.MCP.Session.DependencyInjection;

public static class SessionServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSessionModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<SessionCoordinationOptions>()
            .Bind(configuration.GetSection(SessionCoordinationOptions.SectionName))
            .Validate(
                options =>
                    PositiveDuration(options.ClaimLeaseMinutes, 60)
                    && PositiveDuration(options.SnapshotLifetimeMinutes, 60),
                "SessionCoordination lease and snapshot durations must be finite and positive."
            )
            .ValidateOnStart();
        services
            .AddOptions<SessionLimitsOptions>()
            .Bind(configuration.GetSection(SessionLimitsOptions.SectionName))
            .Validate(
                options =>
                    options.MaxContentBytes > 0
                    && options.MaxRecordBytes >= options.MaxContentBytes
                    && options.MaxJsonDepth > 0
                    && options.MaxMutationBatch > 0
                    && options.MaxRecallResults > 0,
                "Session limits must be positive and the record limit must not be smaller than the content limit."
            )
            .ValidateOnStart();

        services.TryAddSingleton<SessionRequestValidator>();
        services.TryAddSingleton<SessionContextGuard>();
        services.TryAddSingleton<SharedSessionLifecycleService>();
        services.TryAddSingleton<SessionCoordinationService>();
        services.TryAddSingleton<SessionResumeService>();
        services.TryAddSingleton<ISessionLifecycleCapabilities>(provider =>
            provider.GetRequiredService<SharedSessionLifecycleService>()
        );
        services.TryAddSingleton<ISessionCoordinationCapabilities>(provider =>
            provider.GetRequiredService<SessionCoordinationService>()
        );
        services.TryAddSingleton<ISessionResumeCapabilities>(provider =>
            provider.GetRequiredService<SessionResumeService>()
        );
        return services;
    }

    private static bool PositiveDuration(double value, double secondsPerUnit) =>
        double.IsFinite(value)
        && value > 0
        && value <= TimeSpan.MaxValue.TotalSeconds / secondsPerUnit;
}
