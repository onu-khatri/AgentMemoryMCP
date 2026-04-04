using AgentSession.MCP.Models;
using AgentSession.MCP.Options;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

public sealed class SessionStoragePathBuilder
{
    private readonly SessionStorageOptions _options;

    public SessionStoragePathBuilder(IOptions<SessionStorageOptions> options)
    {
        _options = options.Value;
    }

    public string RootPath => _options.RootPath;

    public AgentSessionPaths Build(string sessionId)
    {
        var sessionPath = Path.Combine(_options.RootPath, sessionId);
        return new AgentSessionPaths
        {
            SessionPath = sessionPath,
            StateFilePath = Path.Combine(sessionPath, AgentSessionState.AgentSessionStateFileName),
            MemoryFilePath = Path.Combine(sessionPath, AgentSessionState.AgentMemoryFileName),
            LogFilePath = Path.Combine(sessionPath, AgentSessionState.AgentSessionLogFileName),
            ArtifactDirectoryPath = Path.Combine(sessionPath, "artifacts")
        };
    }
}
