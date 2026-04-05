using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Models;
using AgentSession.MCP.Tools;
using AgentSession.MCP.Helpers;
using Microsoft.Extensions.Logging;

namespace AgentSession.MCP.Services;

public sealed class SessionMemoryService
{
    private static readonly HashSet<string> LogLevels = ["Info", "Warning", "Error", "Debug"];
    private readonly IAgentSessionStore _store;
    private readonly ILogger<SessionMemoryService> _logger;

    public SessionMemoryService(IAgentSessionStore store, ILogger<SessionMemoryService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<ReadAgentMemoryResponse> ReadAgentMemoryAsync(ReadAgentMemoryRequest request, CancellationToken cancellationToken)
    {
        var sessionId = AgentSessionValidation.ValidateSessionId(request.SessionId);

        var memory = await _store.ReadMemoryAsync(sessionId, cancellationToken);
        var artifacts = await _store.ListArtifactsAsync(sessionId, cancellationToken);
        var logs = await _store.ReadLogsAsync(sessionId, Math.Clamp(request.LatestLogEntries, 1, 200), cancellationToken);
        var state = await _store.GetSessionStateAsync(sessionId, cancellationToken);

        return new ReadAgentMemoryResponse
        {
            SessionId = sessionId,
            Memory = memory,
            Artifacts = artifacts.Select(AgentSessionContractMapper.ToArtifactItem).ToList(),
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
        var sessionId = AgentSessionValidation.ValidateSessionId(request.SessionId);
        AgentSessionValidation.RequireValue(request.Content, "content");

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
        var sessionId = AgentSessionValidation.ValidateSessionId(request.SessionId);
        var message = AgentSessionValidation.RequireValue(request.Message, "message");
        var agentName = AgentSessionValidation.RequireValue(request.AgentName, "agentName");

        var level = string.IsNullOrWhiteSpace(request.Level) ? "Info" : request.Level.Trim();
        if (!LogLevels.Contains(level))
        {
            throw new ValidationException("level must be one of Info, Warning, Error, Debug.");
        }

        var entry = new AgentSessionLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Message = message,
            AgentName = agentName,
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
}
