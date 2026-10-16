using OnuObservability.Hosting.Composition;
using OnuObservability.Model;

namespace AgentSession.MCP.Infrastructure.Observability;

public static class InfrastructureObservabilityExtensions
{
    public static OnuObservabilityBuilder AddAgentSessionInfrastructureTelemetry(
        this OnuObservabilityBuilder builder
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var operation in DependencyTelemetrySchema.Operations)
            builder.AddOperation(operation);
        foreach (var error in DependencyTelemetrySchema.ErrorCodes)
            builder.AddError(new ErrorCode(error));
        return builder;
    }
}
