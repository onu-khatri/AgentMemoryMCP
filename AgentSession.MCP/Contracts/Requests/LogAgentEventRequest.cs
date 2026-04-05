using System.ComponentModel;

namespace AgentSession.MCP.Tools;

public sealed class LogAgentEventRequest
{
    [Description("Session id to write the event to.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("Log message content.")]
    public string Message { get; set; } = string.Empty;

    [Description("Agent name creating the event.")]
    public string AgentName { get; set; } = string.Empty;

    [Description("Severity level: Info, Warning, Error, Debug.")]
    public string Level { get; set; } = "Info";

    [Description("Optional event category.")]
    public string? EventType { get; set; }

    [Description("Optional correlation id.")]
    public string? CorrelationId { get; set; }
}

