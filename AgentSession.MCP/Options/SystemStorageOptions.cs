namespace AgentSession.MCP.Options;

public sealed class SystemStorageOptions
{
    public const string SectionName = "SystemStorage";

    public string Root { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "AgentMemory");
}
