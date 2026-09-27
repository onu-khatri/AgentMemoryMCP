namespace AgentSession.MCP.Options;

public sealed class RepositoryOptions
{
    public const string SectionName = "Repository";

    // Explicit identity stays stable when the checkout moves. No path-derived fallback.
    public string Id { get; set; } = string.Empty;
}
