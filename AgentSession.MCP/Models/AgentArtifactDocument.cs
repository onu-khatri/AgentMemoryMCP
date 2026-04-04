namespace AgentSession.MCP.Models;

public sealed class AgentArtifactDocument
{
    public AgentArtifactMetadata Metadata { get; set; } = new();

    public string Content { get; set; } = string.Empty;
}
