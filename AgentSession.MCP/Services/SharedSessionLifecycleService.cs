using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models;
using AgentSession.MCP.Options;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

public sealed class SharedSessionLifecycleService(
    ManagedTransactionStore store,
    MemoryStoragePaths paths,
    TimeProvider time,
    IOptions<MemoryPolicyOptions> options
)
{
    public async Task<SharedSessionActivation> ActivateAsync(
        ActivateSharedSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        ManagedStoragePathResolver.RequireIdentifier(request.SessionId);
        ManagedStoragePathResolver.RequireIdentifier(request.ActorId);
        ManagedStoragePathResolver.RequireIdentifier(request.OperationId);
        var file = new ManagedFile(
            ManagedArea.Session,
            request.SessionId,
            ["coordination", "metadata.json"]
        );
        var existing = await store.ReadAsync(file, cancellationToken);
        if (existing is not null)
            return ReadActivation(existing, request.SessionId);
        var metadata = new SessionCoordinationMetadata
        {
            RepositoryId = paths.RepositoryId,
            SessionId = request.SessionId,
            Sequence = 1,
            Changes =
            [
                new(1, request.ActorId, "session-created", request.SessionId, 1, time.GetUtcNow()),
            ],
        };
        try
        {
            await store.CommitAsync(
                "activate-"
                    + ManagedTransactionStore.Hash(request.SessionId + "/" + request.OperationId),
                [new(file, JsonSerializer.Serialize(metadata, MemoryJson.Options), null)],
                cancellationToken
            );
        }
        catch (ValidationException)
        {
            existing = await store.ReadAsync(file, cancellationToken);
            if (existing is null)
                throw;
            return ReadActivation(existing, request.SessionId);
        }
        return new(request.SessionId, metadata.Sequence);
    }

    public async Task<SharedSessionList> ListAsync(
        ListSharedSessionsRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request.PageSize < 1 || request.PageSize > options.Value.Limits.MaxRecallResults)
            throw new ValidationException("Session page size exceeds the configured bounds.");
        var ids = await store.ListSessionIdsAsync(
            request.PageSize + 1,
            request.After,
            cancellationToken
        );
        var hasMore = ids.Count > request.PageSize;
        var page = ids.Take(request.PageSize).ToArray();
        return new(page, hasMore, hasMore ? page[^1] : null);
    }

    private SharedSessionActivation ReadActivation(string json, string id)
    {
        var metadata = JsonSerializer.Deserialize<SessionCoordinationMetadata>(
            json,
            MemoryJson.Options
        )!;
        if (
            metadata.SchemaVersion != 1
            || metadata.RepositoryId != paths.RepositoryId
            || metadata.SessionId != id
        )
            throw new InvalidDataException("Session metadata version or identity mismatch.");
        return new(id, metadata.Sequence);
    }
}
