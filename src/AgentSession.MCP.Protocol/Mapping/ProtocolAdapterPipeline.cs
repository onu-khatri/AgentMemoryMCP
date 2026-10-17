using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Core.Security;
using AgentSession.MCP.Protocol.Errors;
using AgentSession.MCP.Protocol.Filters;
using AgentSession.MCP.Protocol.Validation;

namespace AgentSession.MCP.Protocol.Mapping;

public sealed class ProtocolAdapterPipeline(
    IPlatformRequestContextAccessor contextAccessor,
    ProtocolBoundsOptions? bounds = null)
{
    private readonly ProtocolBoundsOptions bounds = bounds ?? new();

    public Task<T> ExecuteAsync<T>(
        string capability,
        object? request,
        Func<PlatformRequestContext, CancellationToken, Task<T>> call,
        CancellationToken cancellationToken) => ExecuteCoreAsync(capability, request, call, cancellationToken);

    public Task<T> ExecuteAsync<T>(
        string capability,
        Func<PlatformRequestContext, CancellationToken, Task<T>> call,
        CancellationToken cancellationToken) => ExecuteCoreAsync(capability, null, call, cancellationToken, false);

    private async Task<T> ExecuteCoreAsync<T>(
        string capability,
        object? request,
        Func<PlatformRequestContext, CancellationToken, Task<T>> call,
        CancellationToken cancellationToken,
        bool requiresRequest = true)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (requiresRequest)
                ProtocolInputValidation.ValidateRequest(request as object, bounds, "request");

            var context = contextAccessor.Current;
            CapabilityAuthorizationFilter.Require(context, capability);
            cancellationToken.ThrowIfCancellationRequested();
            var result = await call(context, cancellationToken).WaitAsync(bounds.Timeout, cancellationToken);
            return result;
        }
        catch (Exception error) when (error is not ProtocolErrorException)
        {
            throw ProtocolErrorMapping.ToMcpException(error);
        }
    }
}

internal sealed class ProtocolErrorException : Exception
{
    public ProtocolErrorException(string message) : base(message) { }
}
