namespace AgentSession.MCP.Helpers;

public sealed class ValidationException : Exception
{
    public string Code { get; }

    public ValidationException(string message, string code = "invalid_request")
        : base(message)
    {
        Code = code;
    }
}
