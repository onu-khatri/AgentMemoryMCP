namespace AgentSession.MCP.Models;

public sealed class AgentSessionPaths
{
    public required string SessionPath { get; init; }

    public required string StateFilePath { get; init; }

    public required string MemoryFilePath { get; init; }

    public required string LogFilePath { get; init; }

    public required string ArtifactDirectoryPath { get; init; }
}
