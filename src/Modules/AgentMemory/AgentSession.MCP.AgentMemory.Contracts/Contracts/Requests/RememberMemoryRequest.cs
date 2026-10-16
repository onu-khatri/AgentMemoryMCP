using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace AgentSession.MCP.Contracts;

public sealed class RememberMemoryRequest
{
    public required string OperationId { get; set; }
    public required RememberTier Tier { get; set; }
    [MaxLength(65_536)]
    public required string Content { get; set; }
    [MaxLength(4_096)]
    public string Title { get; set; } = string.Empty;
    public string? RepositoryId { get; set; }
    public string? SessionId { get; set; }
    public string? TaskId { get; set; }
    public string? ParentStepId { get; set; }
    public string? AgentId { get; set; }
    public string Category { get; set; } = "observation";
    public string? DecisionArea { get; set; }
    [MaxLength(100)]
    public List<string> Tags { get; set; } = [];
    public string SourceType { get; set; } = "agent";
    public string? SourceReference { get; set; }
    public JsonElement? StructuredData { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset? ReviewAfterUtc { get; set; }
    public bool Protected { get; set; }
}
