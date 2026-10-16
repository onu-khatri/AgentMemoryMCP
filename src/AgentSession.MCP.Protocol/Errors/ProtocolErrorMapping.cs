using AgentSession.MCP.Core.Errors;
using AgentSession.MCP.Helpers;
using ModelContextProtocol;

namespace AgentSession.MCP.Protocol.Errors;

public static class ProtocolErrorMapping
{
    public static McpException ToMcpException(Exception error) => error switch
    {
        McpException mcp => mcp,
        ValidationException validation => new McpException($"validation_error: {validation.Code}"),
        UnauthorizedException => new McpException("unauthorized: client authentication is required."),
        ForbiddenException => new McpException("forbidden: capability access is not permitted."),
        OperationCanceledException => new McpException("cancelled: request was cancelled."),
        TimeoutException => new McpException("timeout: dependency did not complete in time."),
        InvalidDataException => new McpException("dependency_unavailable: managed data is unavailable."),
        PlatformException platform => new McpException(ToStableMessage(platform.Error)),
        _ => new McpException("unexpected: the operation could not be completed.")
    };

    private static string ToStableMessage(PlatformError error) =>
        $"{error.Category.ToString().ToLowerInvariant()}: {error.Code}";
}

public sealed class UnauthorizedException : Exception;
public sealed class ForbiddenException : Exception;
