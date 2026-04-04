using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace AgentSession.MCP.Models;

public sealed class AgentArtifactMetadata
{
    [YamlMember(Alias = "name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "intended_use")]
    [JsonPropertyName("intended_use")]
    public string IntendedUse { get; set; } = string.Empty;

    [YamlMember(Alias = "content_type")]
    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = "text/markdown";

    [YamlMember(Alias = "created_at")]
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [YamlMember(Alias = "updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [YamlMember(Alias = "tags")]
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [YamlMember(Alias = "file_name")]
    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [YamlMember(Alias = "final_plan_details")]
    [JsonPropertyName("final_plan_details")]
    public FinalPlanDetails? FinalPlanDetails { get; set; }
}
