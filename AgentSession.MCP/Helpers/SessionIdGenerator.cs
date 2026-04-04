namespace AgentSession.MCP.Helpers;

public static class SessionIdGenerator
{
    public static string NewReadableSessionId()
    {
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var suffix = Convert.ToHexString(Guid.NewGuid().ToByteArray()[..2]).ToLowerInvariant();
        return $"session-{stamp}-{suffix}";
    }
}
