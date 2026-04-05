using System.ComponentModel;

namespace AgentSession.MCP.Tools;

public sealed class ReadAgentArtifactRequest
{
    [Description("Session id containing the artifact.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("Artifact name to read.")]
    public string ArtifactName { get; set; } = string.Empty;
}

