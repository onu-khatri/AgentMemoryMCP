namespace AgentSession.MCP.Helpers;

public sealed class ValidationException : Exception
{
    public ValidationException(string message)
        : base(message)
    {
    }
}
