using AgentSession.MCP.Core.Context;

namespace AgentSession.MCP.Core.Execution;

public sealed record ExecutionMetadata(
    string? OperationId,
    string? IdempotencyKey,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? DeadlineUtc
);

public sealed record CapabilityExecutionContext
{
    public CapabilityExecutionContext(PlatformRequestContext request, ExecutionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(metadata);
        Request = request;
        Metadata = metadata;
    }

    public PlatformRequestContext Request { get; }
    public ExecutionMetadata Metadata { get; }
}
