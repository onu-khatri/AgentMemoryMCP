using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AgentSession.MCP.Tools;

public sealed class ListAgentSessionsItem
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("current_state_summary")]
    public string? CurrentStateSummary { get; set; }

    [JsonPropertyName("artifact_count")]
    public int ArtifactCount { get; set; }

    [JsonPropertyName("last_log_timestamp")]
    public DateTimeOffset? LastLogTimestamp { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class CreateOrActivateSessionRequest
{
    [Description("Optional deterministic session id. If omitted, a human-readable unique id is generated.")]
    public string? SessionId { get; set; }

    [Description("Optional initial state summary for this session.")]
    public string? InitialState { get; set; }

    [Description("Optional agent name activating this session.")]
    public string? AgentName { get; set; }
}

public sealed class ReadAgentMemoryRequest
{
    [Description("Session id to read memory from.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("How many recent log entries to include in summary.")]
    public int LatestLogEntries { get; set; } = 10;
}

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

public sealed class ReadAgentArtifactRequest
{
    [Description("Session id containing the artifact.")]
    public string SessionId { get; set; } = string.Empty;

    [Description("Artifact name to read.")]
    public string ArtifactName { get; set; } = string.Empty;
}

public sealed class ListAgentArtifactsRequest
{
    [Description("Session id containing artifacts.")]
    public string SessionId { get; set; } = string.Empty;
}

public sealed class ReadAgentMemoryResponse
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("memory")]
    public string Memory { get; set; } = string.Empty;

    [JsonPropertyName("artifacts")]
    public IReadOnlyCollection<ArtifactItem> Artifacts { get; set; } = [];

    [JsonPropertyName("latest_logs")]
    public IReadOnlyCollection<LogItem> LatestLogs { get; set; } = [];

    [JsonPropertyName("metadata")]
    public SessionMetadataItem Metadata { get; set; } = new();
}

public sealed class AppendAgentMemoryResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("memory_size_bytes")]
    public long MemorySizeBytes { get; set; }
}

public sealed class OperationResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class CreateAgentArtifactResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public ArtifactItem Metadata { get; set; } = new();
}

public sealed class ReadAgentArtifactResponse
{
    [JsonPropertyName("metadata")]
    public ArtifactItem Metadata { get; set; } = new();

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class ListAgentArtifactsResponse
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("artifacts")]
    public IReadOnlyCollection<ArtifactItem> Artifacts { get; set; } = [];
}

public sealed class ArtifactItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("intended_use")]
    public string IntendedUse { get; set; } = string.Empty;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("tags")]
    public IReadOnlyCollection<string> Tags { get; set; } = [];

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;
}

public sealed class SessionMetadataItem
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("current_state")]
    public string? CurrentState { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class LogItem
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;

    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; set; }
}
