using System.Text.Json;

namespace AgentSession.MCP.Models;

public sealed class SharedSessionArtifact
{
    public int SchemaVersion { get; set; } = 1;
    public required string Id { get; set; }
    public required SessionArtifactKind Kind { get; set; }
    public required string ActorId { get; set; }
    public string? TaskId { get; set; }
    public required string Title { get; set; }
    public JsonElement? Data { get; set; }
    public SessionWorkingContext? Context { get; set; }
    public List<string> Evidence { get; set; } = [];
    public long Revision { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
