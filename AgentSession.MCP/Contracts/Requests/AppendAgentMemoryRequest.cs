using System.ComponentModel;

namespace AgentSession.MCP.Tools;

public sealed class AppendAgentMemoryRequest
{
    [Description("Session id to append memory into.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("Durable memory content to append.")]
    public string Content { get; set; } = string.Empty;

    [Description("Optional agent name authoring this memory.")]
    public string? AgentName { get; set; }

    [Description("Optional markdown section title.")]
    public string? SectionTitle { get; set; }
}

