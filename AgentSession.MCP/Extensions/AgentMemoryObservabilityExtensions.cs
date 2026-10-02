using AgentSession.MCP.Observability;
using OnuObservability.Hosting;
using OnuObservability.Model;

namespace AgentSession.MCP.Extensions;

internal static class AgentMemoryObservabilityExtensions
{
    public static OnuObservabilityBuilder AddAgentMemoryTelemetry(
        this OnuObservabilityBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var operation in AgentMemoryTelemetrySchema.Operations)
        {
            builder.AddOperation(operation);
        }

        foreach (var error in TelemetrySchema.ErrorCodes)
        {
            builder.AddError(new ErrorCode(error));
        }

        builder.AddOutboundHttpClientInstrumentation(static request =>
            request.RequestUri is { IsLoopback: true } uri
            && uri.AbsolutePath is "/api/tags" or "/api/embed");

        return builder;
    }
}
