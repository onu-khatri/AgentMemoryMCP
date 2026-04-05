using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Tools;

namespace AgentSession.MCP.Services;

public sealed class SessionArtifactService
{
    private readonly IAgentSessionStore _store;
    private readonly SessionStoragePathBuilder _pathBuilder;

    public SessionArtifactService(IAgentSessionStore store, SessionStoragePathBuilder pathBuilder)
    {
        _store = store;
        _pathBuilder = pathBuilder;
    }

    public async Task<CreateAgentArtifactResponse> CreateAgentArtifactAsync(CreateAgentArtifactRequest request, CancellationToken cancellationToken)
    {
        var sessionId = AgentSessionValidation.ValidateSessionId(request.SessionId);
        AgentSessionValidation.RequireValue(request.ArtifactName, "artifactName");
        AgentSessionValidation.RequireValue(request.Description, "description");
        AgentSessionValidation.RequireValue(request.IntendedUse, "intendedUse");

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
            Metadata = AgentSessionContractMapper.ToArtifactItem(doc.Metadata)
        };
    }

    public async Task<ReadAgentArtifactResponse> ReadAgentArtifactAsync(ReadAgentArtifactRequest request, CancellationToken cancellationToken)
    {
        var sessionId = AgentSessionValidation.ValidateSessionId(request.SessionId);
        AgentSessionValidation.RequireValue(request.ArtifactName, "artifactName");

        var artifact = await _store.ReadArtifactAsync(sessionId, request.ArtifactName, cancellationToken);
        return new ReadAgentArtifactResponse
        {
            Metadata = AgentSessionContractMapper.ToArtifactItem(artifact.Metadata),
            Content = artifact.Content
        };
    }

    public async Task<ListAgentArtifactsResponse> ListAgentArtifactsAsync(ListAgentArtifactsRequest request, CancellationToken cancellationToken)
    {
        var sessionId = AgentSessionValidation.ValidateSessionId(request.SessionId);
        var artifacts = await _store.ListArtifactsAsync(sessionId, cancellationToken);

        return new ListAgentArtifactsResponse
        {
            SessionId = sessionId,
            Artifacts = artifacts.Select(AgentSessionContractMapper.ToArtifactItem).ToList()
        };
    }
}
