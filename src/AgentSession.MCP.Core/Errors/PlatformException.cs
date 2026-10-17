namespace AgentSession.MCP.Core.Errors;

public sealed class PlatformException(PlatformError error) : Exception(error.Message)
{
    public PlatformError Error { get; } = error ?? throw new ArgumentNullException(nameof(error));
}
