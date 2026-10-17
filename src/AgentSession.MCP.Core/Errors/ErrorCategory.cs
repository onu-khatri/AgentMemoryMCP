namespace AgentSession.MCP.Core.Errors;

public enum ErrorCategory
{
    ValidationError,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    RateLimited,
    DependencyUnavailable,
    Timeout,
    Cancelled,
    Unexpected,
}
