using System.ComponentModel;

namespace AgentSession.MCP.Tools;

public sealed class CreateAgentArtifactRequest
{
    [Description("Session id containing the artifact.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("Artifact name (filesystem-safe).")]
    public string ArtifactName { get; set; } = string.Empty;

    [Description("Human-readable artifact description.")]
    public string Description { get; set; } = string.Empty;

    [Description("How this artifact should be used.")]
    public string IntendedUse { get; set; } = string.Empty;

    [Description("Artifact body content.")]
    public string Content { get; set; } = string.Empty;

    [Description("Optional content type (defaults to text/markdown).")]
    public string? ContentType { get; set; }

    [Description("Optional tags.")]
    public IReadOnlyCollection<string>? Tags { get; set; }

    [Description("Allow overwrite if artifact already exists.")]
    public bool Overwrite { get; set; }
}

