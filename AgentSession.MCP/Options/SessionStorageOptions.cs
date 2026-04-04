namespace AgentSession.MCP.Options;

/// <summary>
/// Configures local storage for agent sessions.
/// </summary>
public sealed class SessionStorageOptions
{
    public string RootPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AgentMemory",
        "sessions");
}
