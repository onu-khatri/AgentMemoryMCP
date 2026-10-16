namespace AgentSession.MCP.Core.Errors;

public sealed record PlatformError
{
    public PlatformError(
        ErrorCategory category,
        string code,
        string message,
        bool isTransient = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Category = category;
        Code = code;
        Message = message;
        IsTransient = isTransient;
    }

    public ErrorCategory Category { get; }
    public string Code { get; }
    public string Message { get; }
    public bool IsTransient { get; }
}
