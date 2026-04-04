using System.Collections.Concurrent;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Models;
using Microsoft.Extensions.Logging;

namespace AgentSession.MCP.Services;

public sealed class FileAgentSessionStore : IAgentSessionStore
{
    private readonly IFileSystem _fileSystem;
    private readonly IYamlSerializer _yamlSerializer;
    private readonly SessionStoragePathBuilder _pathBuilder;
    private readonly ILogger<FileAgentSessionStore> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sessionLocks = new(StringComparer.Ordinal);

    public FileAgentSessionStore(
        IFileSystem fileSystem,
        IYamlSerializer yamlSerializer,
        SessionStoragePathBuilder pathBuilder,
        ILogger<FileAgentSessionStore> logger)
    {
        _fileSystem = fileSystem;
        _yamlSerializer = yamlSerializer;
        _pathBuilder = pathBuilder;
        _logger = logger;

        _fileSystem.CreateDirectory(_pathBuilder.RootPath);
    }

    public async Task<IReadOnlyCollection<AgentSessionSummary>> ListSessionsAsync(CancellationToken cancellationToken)
    {
        _fileSystem.CreateDirectory(_pathBuilder.RootPath);

        var results = new List<AgentSessionSummary>();
        foreach (var sessionPath in _fileSystem.EnumerateDirectories(_pathBuilder.RootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sessionId = _fileSystem.GetFileName(sessionPath);
            if (string.IsNullOrWhiteSpace(sessionId) || !NameSanitizer.IsSafePathSegment(sessionId))
            {
                continue;
            }

            try
            {
                var state = await GetSessionStateAsync(sessionId, cancellationToken);
                var artifacts = await ListArtifactsAsync(sessionId, cancellationToken);
                var logs = await ReadLogsAsync(sessionId, 1, cancellationToken);

                results.Add(new AgentSessionSummary
                {
                    SessionId = state.SessionId,
                    CreatedAt = state.CreatedAt,
                    UpdatedAt = state.UpdatedAt,
                    CurrentStateSummary = state.CurrentState,
                    ArtifactCount = artifacts.Count,
                    LastLogTimestamp = logs.FirstOrDefault()?.Timestamp,
                    Description = state.Description
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to inspect session folder {SessionPath}", sessionPath);
            }
        }

        return results
            .OrderByDescending(item => item.UpdatedAt)
            .ToList();
    }

    public async Task<(AgentSessionState Session, bool Created, AgentSessionPaths Paths)> CreateOrActivateSessionAsync(
        string? requestedSessionId,
        string? initialState,
        string? agentName,
        CancellationToken cancellationToken)
    {
        var sessionId = string.IsNullOrWhiteSpace(requestedSessionId)
            ? SessionIdGenerator.NewReadableSessionId()
            : NameSanitizer.SanitizeIdentifier(requestedSessionId);

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ValidationException("sessionId is invalid after sanitization.");
        }

        var paths = _pathBuilder.Build(sessionId);
        var gate = GetLock(sessionId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            _fileSystem.CreateDirectory(paths.SessionPath);
            _fileSystem.CreateDirectory(paths.ArtifactDirectoryPath);

            var exists = _fileSystem.FileExists(paths.StateFilePath);
            var now = DateTimeOffset.UtcNow;

            var state = exists
                ? await ReadYamlFileAsync<AgentSessionState>(paths.StateFilePath, cancellationToken) ?? throw new InvalidOperationException("Session state is malformed.")
                : new AgentSessionState
                {
                    SessionId = sessionId,
                    CreatedAt = now,
                    UpdatedAt = now,
                    AgentName = string.IsNullOrWhiteSpace(agentName) ? "unspecified" : agentName.Trim(),
                    CurrentState = initialState,
                    Description = initialState
                };

            state.SessionId = sessionId;
            if (!string.IsNullOrWhiteSpace(initialState))
            {
                state.CurrentState = initialState;
                state.Description ??= initialState;
            }

            if (!string.IsNullOrWhiteSpace(agentName))
            {
                state.AgentName = agentName.Trim();
            }

            state.UpdatedAt = DateTimeOffset.UtcNow;

            await WriteYamlFileAsync(paths.StateFilePath, state, cancellationToken);
            await EnsureTextFileExistsAsync(paths.MemoryFilePath, "# Agent Memory\n\n", cancellationToken);
            await EnsureTextFileExistsAsync(paths.LogFilePath, "[]\n", cancellationToken);

            return (state, !exists, paths);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string> ReadMemoryAsync(string sessionId, CancellationToken cancellationToken)
    {
        var sanitized = ValidateExistingSession(sessionId);
        var paths = _pathBuilder.Build(sanitized);

        return _fileSystem.FileExists(paths.MemoryFilePath)
            ? await _fileSystem.ReadAllTextAsync(paths.MemoryFilePath, cancellationToken)
            : string.Empty;
    }

    public async Task<long> AppendMemoryAsync(string sessionId, string content, string? agentName, string? sectionTitle, CancellationToken cancellationToken)
    {
        var sanitized = ValidateExistingSession(sessionId);
        var paths = _pathBuilder.Build(sanitized);

        var gate = GetLock(sanitized);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var existing = _fileSystem.FileExists(paths.MemoryFilePath)
                ? await _fileSystem.ReadAllTextAsync(paths.MemoryFilePath, cancellationToken)
                : "# Agent Memory\n\n";

            var title = string.IsNullOrWhiteSpace(sectionTitle) ? "Memory Update" : sectionTitle.Trim();
            var who = string.IsNullOrWhiteSpace(agentName) ? "unspecified" : agentName.Trim();
            var stamp = DateTimeOffset.UtcNow.ToString("u");

            var entry = $"\n## {title}\n- timestamp: {stamp}\n- agent: {who}\n\n{content.Trim()}\n";
            var updated = existing + entry;

            await _fileSystem.WriteAllTextAtomicAsync(paths.MemoryFilePath, updated, cancellationToken);
            await TouchSessionUpdatedAsync(paths, cancellationToken);
            return updated.Length;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task AppendLogAsync(string sessionId, AgentSessionLogEntry entry, CancellationToken cancellationToken)
    {
        var sanitized = ValidateExistingSession(sessionId);
        var paths = _pathBuilder.Build(sanitized);

        var gate = GetLock(sanitized);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var logs = await ReadYamlFileAsync<List<AgentSessionLogEntry>>(paths.LogFilePath, cancellationToken) ?? [];
            logs.Add(entry);
            await WriteYamlFileAsync(paths.LogFilePath, logs, cancellationToken);
            await TouchSessionUpdatedAsync(paths, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<AgentArtifactDocument> CreateOrUpdateArtifactAsync(
        string sessionId,
        string artifactName,
        string description,
        string intendedUse,
        string content,
        string? contentType,
        IReadOnlyCollection<string>? tags,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var sanitizedSession = ValidateExistingSession(sessionId);
        var sanitizedArtifact = NameSanitizer.SanitizeIdentifier(artifactName);
        if (string.IsNullOrWhiteSpace(sanitizedArtifact))
        {
            throw new ValidationException("artifactName is invalid.");
        }

        var paths = _pathBuilder.Build(sanitizedSession);
        var gate = GetLock(sanitizedSession);
        await gate.WaitAsync(cancellationToken);
        try
        {
            _fileSystem.CreateDirectory(paths.ArtifactDirectoryPath);
            var fileName = $"{sanitizedArtifact}.md";
            var artifactPath = Path.Combine(paths.ArtifactDirectoryPath, fileName);

            AgentArtifactMetadata metadata;
            if (_fileSystem.FileExists(artifactPath))
            {
                if (!overwrite)
                {
                    throw new ValidationException("Artifact already exists. Set overwrite=true to replace.");
                }

                var existing = await _fileSystem.ReadAllTextAsync(artifactPath, cancellationToken);
                var existingDoc = FrontMatterHelper.ParseArtifactDocument(existing, _yamlSerializer);
                metadata = existingDoc.Metadata;
                metadata.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                metadata = new AgentArtifactMetadata
                {
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    FileName = fileName
                };
            }

            metadata.Name = sanitizedArtifact;
            metadata.Description = description.Trim();
            metadata.IntendedUse = intendedUse.Trim();
            metadata.ContentType = string.IsNullOrWhiteSpace(contentType) ? "text/markdown" : contentType.Trim();
            metadata.Tags = tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];

            var markdown = FrontMatterHelper.ToMarkdown(metadata, content, _yamlSerializer);
            await _fileSystem.WriteAllTextAtomicAsync(artifactPath, markdown, cancellationToken);
            await TouchSessionUpdatedAsync(paths, cancellationToken);

            return new AgentArtifactDocument
            {
                Metadata = metadata,
                Content = content
            };
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<AgentArtifactDocument> ReadArtifactAsync(string sessionId, string artifactName, CancellationToken cancellationToken)
    {
        var sanitizedSession = ValidateExistingSession(sessionId);
        var sanitizedArtifact = NameSanitizer.SanitizeIdentifier(artifactName);
        if (string.IsNullOrWhiteSpace(sanitizedArtifact))
        {
            throw new ValidationException("artifactName is invalid.");
        }

        var paths = _pathBuilder.Build(sanitizedSession);
        var artifactPath = Path.Combine(paths.ArtifactDirectoryPath, $"{sanitizedArtifact}.md");
        if (!_fileSystem.FileExists(artifactPath))
        {
            throw new FileNotFoundException($"Artifact '{sanitizedArtifact}' was not found.");
        }

        var markdown = await _fileSystem.ReadAllTextAsync(artifactPath, cancellationToken);
        return FrontMatterHelper.ParseArtifactDocument(markdown, _yamlSerializer);
    }

    public async Task<IReadOnlyCollection<AgentArtifactMetadata>> ListArtifactsAsync(string sessionId, CancellationToken cancellationToken)
    {
        var sanitized = ValidateExistingSession(sessionId);
        var paths = _pathBuilder.Build(sanitized);

        var artifacts = new List<AgentArtifactMetadata>();
        foreach (var filePath in _fileSystem.EnumerateFiles(paths.ArtifactDirectoryPath, "*.md"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var raw = await _fileSystem.ReadAllTextAsync(filePath, cancellationToken);
                var doc = FrontMatterHelper.ParseArtifactDocument(raw, _yamlSerializer);
                artifacts.Add(doc.Metadata);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping malformed artifact {ArtifactPath}", filePath);
            }
        }

        return artifacts
            .OrderByDescending(a => a.UpdatedAt)
            .ToList();
    }

    public async Task<IReadOnlyCollection<AgentSessionLogEntry>> ReadLogsAsync(string sessionId, int takeLast, CancellationToken cancellationToken)
    {
        var sanitized = ValidateExistingSession(sessionId);
        var paths = _pathBuilder.Build(sanitized);

        var logs = await ReadYamlFileAsync<List<AgentSessionLogEntry>>(paths.LogFilePath, cancellationToken) ?? [];
        if (takeLast <= 0)
        {
            return logs;
        }

        return logs.TakeLast(takeLast).ToList();
    }

    public async Task<AgentSessionState> GetSessionStateAsync(string sessionId, CancellationToken cancellationToken)
    {
        var sanitized = ValidateExistingSession(sessionId);
        var paths = _pathBuilder.Build(sanitized);

        var state = await ReadYamlFileAsync<AgentSessionState>(paths.StateFilePath, cancellationToken);
        if (state is null)
        {
            throw new InvalidOperationException($"Session state for '{sanitized}' is missing or malformed.");
        }

        return state;
    }

    private SemaphoreSlim GetLock(string sessionId)
        => _sessionLocks.GetOrAdd(sessionId, static _ => new SemaphoreSlim(1, 1));

    private string ValidateExistingSession(string sessionId)
    {
        var sanitized = NameSanitizer.SanitizeIdentifier(sessionId);
        if (string.IsNullOrWhiteSpace(sanitized) || !NameSanitizer.IsSafePathSegment(sanitized))
        {
            throw new ValidationException("sessionId is invalid.");
        }

        var paths = _pathBuilder.Build(sanitized);
        if (!_fileSystem.DirectoryExists(paths.SessionPath))
        {
            throw new DirectoryNotFoundException($"Session '{sanitized}' does not exist.");
        }

        return sanitized;
    }

    private async Task<T?> ReadYamlFileAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!_fileSystem.FileExists(path))
        {
            return default;
        }

        var raw = await _fileSystem.ReadAllTextAsync(path, cancellationToken);
        return _yamlSerializer.Deserialize<T>(raw);
    }

    private Task WriteYamlFileAsync<T>(string path, T value, CancellationToken cancellationToken)
        => _fileSystem.WriteAllTextAtomicAsync(path, _yamlSerializer.Serialize(value), cancellationToken);

    private async Task EnsureTextFileExistsAsync(string path, string initialContent, CancellationToken cancellationToken)
    {
        if (_fileSystem.FileExists(path))
        {
            return;
        }

        await _fileSystem.WriteAllTextAtomicAsync(path, initialContent, cancellationToken);
    }

    private async Task TouchSessionUpdatedAsync(AgentSessionPaths paths, CancellationToken cancellationToken)
    {
        var state = await ReadYamlFileAsync<AgentSessionState>(paths.StateFilePath, cancellationToken)
            ?? throw new InvalidOperationException("Session state not found while updating metadata.");

        state.UpdatedAt = DateTimeOffset.UtcNow;
        await WriteYamlFileAsync(paths.StateFilePath, state, cancellationToken);
    }
}
