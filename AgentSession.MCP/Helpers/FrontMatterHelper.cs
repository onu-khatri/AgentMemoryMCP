using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Models;

namespace AgentSession.MCP.Helpers;

public static class FrontMatterHelper
{
    public static string ToMarkdown(AgentArtifactMetadata metadata, string content, IYamlSerializer serializer)
    {
        var yaml = serializer.Serialize(metadata).Trim();
        return $"---\n{yaml}\n---\n{content}";
    }

    public static AgentArtifactDocument ParseArtifactDocument(string markdown, IYamlSerializer serializer)
    {
        if (!markdown.StartsWith("---", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Artifact does not contain YAML front matter.");
        }

        var end = markdown.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end <= 0)
        {
            throw new InvalidOperationException("Artifact front matter is malformed.");
        }

        var yaml = markdown.Substring(4, end - 4);
        var contentStart = end + 5;
        var content = contentStart < markdown.Length ? markdown[contentStart..] : string.Empty;

        var metadata = serializer.Deserialize<AgentArtifactMetadata>(yaml)
            ?? throw new InvalidOperationException("Artifact metadata could not be deserialized.");

        return new AgentArtifactDocument
        {
            Metadata = metadata,
            Content = content
        };
    }
}
