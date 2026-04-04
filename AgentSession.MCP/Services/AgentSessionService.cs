using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Models;
using AgentSession.MCP.Tools;
using Microsoft.Extensions.Logging;

namespace AgentSession.MCP.Services;

public sealed class AgentSessionService : IAgentSessionService
{
    private static readonly HashSet<string> LogLevels = ["Info", "Warning", "Error", "Debug"];
    private readonly IAgentSessionStore _store;
    private readonly IYamlSerializer _yamlSerializer;
    private readonly SessionStoragePathBuilder _pathBuilder;
    private readonly ILogger<AgentSessionService> _logger;

    public AgentSessionService(
        IAgentSessionStore store,
        IYamlSerializer yamlSerializer,
        SessionStoragePathBuilder pathBuilder,
        ILogger<AgentSessionService> logger)
    {
        _store = store;
        _yamlSerializer = yamlSerializer;
        _pathBuilder = pathBuilder;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<ListAgentSessionsItem>> ListAgentSessionsAsync(CancellationToken cancellationToken)
    {
        var sessions = await _store.ListSessionsAsync(cancellationToken);
        return sessions
            .Select(item => new ListAgentSessionsItem
            {
                SessionId = item.SessionId,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                CurrentStateSummary = item.CurrentStateSummary,
                ArtifactCount = item.ArtifactCount,
                LastLogTimestamp = item.LastLogTimestamp,
                Description = item.Description
            })
            .ToList();
    }

    public async Task<string> CreateOrActivateSessionAsync(CreateOrActivateSessionRequest request, CancellationToken cancellationToken)
    {
        var (session, created, paths) = await _store.CreateOrActivateSessionAsync(
            request.SessionId,
            request.InitialState,
            request.AgentName,
            cancellationToken);

        var payload = new
        {
            session_id = session.SessionId,
            session_path = paths.SessionPath,
            created_or_activated = created ? "created" : "activated",
            current_state = session.CurrentState,
            usage_instruction = session.UsageInstruction,
            available_files = new[]
            {
                AgentSessionState.AgentSessionStateFileName,
                AgentSessionState.AgentMemoryFileName,
                AgentSessionState.AgentSessionLogFileName,
                "artifacts/"
            },
            artifact_folder_path = paths.ArtifactDirectoryPath
        };

        return _yamlSerializer.Serialize(payload);
    }

    public async Task<ReadAgentMemoryResponse> ReadAgentMemoryAsync(ReadAgentMemoryRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);

        var memory = await _store.ReadMemoryAsync(sessionId, cancellationToken);
        var artifacts = await _store.ListArtifactsAsync(sessionId, cancellationToken);
        var logs = await _store.ReadLogsAsync(sessionId, Math.Clamp(request.LatestLogEntries, 1, 200), cancellationToken);
        var state = await _store.GetSessionStateAsync(sessionId, cancellationToken);

        return new ReadAgentMemoryResponse
        {
            SessionId = sessionId,
            Memory = memory,
            Artifacts = artifacts.Select(ToArtifactItem).ToList(),
            LatestLogs = logs.Select(log => new LogItem
            {
                Timestamp = log.Timestamp,
                Message = log.Message,
                AgentName = log.AgentName,
                Level = log.Level,
                EventType = log.EventType,
                CorrelationId = log.CorrelationId
            }).ToList(),
            Metadata = new SessionMetadataItem
            {
                CreatedAt = state.CreatedAt,
                UpdatedAt = state.UpdatedAt,
                AgentName = state.AgentName,
                CurrentState = state.CurrentState,
                Description = state.Description
            }
        };
    }

    public async Task<AppendAgentMemoryResponse> AppendAgentMemoryAsync(AppendAgentMemoryRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ValidationException("content is required.");
        }

        var size = await _store.AppendMemoryAsync(sessionId, request.Content, request.AgentName, request.SectionTitle, cancellationToken);
        return new AppendAgentMemoryResponse
        {
            Success = true,
            Message = "Memory updated successfully.",
            SessionId = sessionId,
            MemorySizeBytes = size
        };
    }

    public async Task<OperationResult> LogAgentEventAsync(LogAgentEventRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ValidationException("message is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AgentName))
        {
            throw new ValidationException("agentName is required.");
        }

        var level = string.IsNullOrWhiteSpace(request.Level) ? "Info" : request.Level.Trim();
        if (!LogLevels.Contains(level))
        {
            throw new ValidationException("level must be one of Info, Warning, Error, Debug.");
        }

        var entry = new AgentSessionLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Message = request.Message.Trim(),
            AgentName = request.AgentName.Trim(),
            Level = level,
            EventType = request.EventType?.Trim(),
            CorrelationId = request.CorrelationId?.Trim()
        };

        await _store.AppendLogAsync(sessionId, entry, cancellationToken);
        _logger.LogInformation("Logged event for session {SessionId}: {Message}", sessionId, entry.Message);

        return new OperationResult
        {
            Success = true,
            Message = "Log entry persisted."
        };
    }

    public async Task<CreateAgentArtifactResponse> CreateAgentArtifactAsync(CreateAgentArtifactRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);
        if (string.IsNullOrWhiteSpace(request.ArtifactName))
        {
            throw new ValidationException("artifactName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ValidationException("description is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IntendedUse))
        {
            throw new ValidationException("intendedUse is required.");
        }

        var doc = await _store.CreateOrUpdateArtifactAsync(
            sessionId,
            request.ArtifactName,
            request.Description,
            request.IntendedUse,
            request.Content,
            request.ContentType,
            request.Tags,
            request.Overwrite,
            cancellationToken);

        return new CreateAgentArtifactResponse
        {
            Success = true,
            Message = "Artifact created successfully.",
            FilePath = Path.Combine(_pathBuilder.Build(sessionId).ArtifactDirectoryPath, doc.Metadata.FileName),
            Metadata = ToArtifactItem(doc.Metadata)
        };
    }

    public async Task<ReadAgentArtifactResponse> ReadAgentArtifactAsync(ReadAgentArtifactRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);
        if (string.IsNullOrWhiteSpace(request.ArtifactName))
        {
            throw new ValidationException("artifactName is required.");
        }

        var artifact = await _store.ReadArtifactAsync(sessionId, request.ArtifactName, cancellationToken);
        return new ReadAgentArtifactResponse
        {
            Metadata = ToArtifactItem(artifact.Metadata),
            Content = artifact.Content
        };
    }

    public async Task<ListAgentArtifactsResponse> ListAgentArtifactsAsync(ListAgentArtifactsRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);
        var artifacts = await _store.ListArtifactsAsync(sessionId, cancellationToken);

        return new ListAgentArtifactsResponse
        {
            SessionId = sessionId,
            Artifacts = artifacts.Select(ToArtifactItem).ToList()
        };
    }

    private static ArtifactItem ToArtifactItem(AgentArtifactMetadata metadata)
        => new()
        {
            Name = metadata.Name,
            Description = metadata.Description,
            IntendedUse = metadata.IntendedUse,
            ContentType = metadata.ContentType,
            CreatedAt = metadata.CreatedAt,
            UpdatedAt = metadata.UpdatedAt,
            Tags = metadata.Tags,
            FileName = metadata.FileName
        };

    private static string ValidateSessionId(string sessionId)
    {
        var sanitized = NameSanitizer.SanitizeIdentifier(sessionId);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ValidationException("sessionId is required and must be filesystem-safe.");
        }

        return sanitized;
    }
}
