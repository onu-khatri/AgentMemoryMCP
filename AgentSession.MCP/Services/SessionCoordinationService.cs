using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models;
using AgentSession.MCP.Options;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

public sealed record SessionMutationResult(
    long Sequence,
    Dictionary<string, long> ArtifactRevisions,
    CoordinatedTask? Task = null
);

internal sealed record SessionOperationResult(string RequestHash, SessionMutationResult Result);

public sealed class SessionCoordinationService(
    ManagedTransactionStore store,
    MemoryStoragePaths paths,
    TimeProvider time,
    IOptions<SessionCoordinationOptions> options,
    MemoryRequestValidator requests
)
{
    private static ManagedFile MetadataFile(string sessionId) =>
        new(ManagedArea.Session, sessionId, ["coordination", "metadata.json"]);

    private static ManagedFile ArtifactFile(string sessionId, string artifactId) =>
        new(ManagedArea.Session, sessionId, ["coordination", "artifacts", artifactId + ".json"]);

    private static ManagedFile ResultFile(string sessionId, string operationId) =>
        new(ManagedArea.Session, sessionId, ["coordination", "results", operationId + ".json"]);

    public async Task<SessionMutationResult> UpdateArtifactsAsync(
        UpdateSessionArtifactsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ValidateIdentity(request.SessionId, request.ActorId, request.OperationId);
        requests.Validate(request);
        var requestHash = ManagedTransactionStore.Hash(
            JsonSerializer.Serialize(request, MemoryJson.Options)
        );
        request = Clone(request);
        requests.ValidateBatch(request.Updates.Count);
        foreach (var update in request.Updates)
        {
            requests.Validate(update.Artifact, content: true);
            ManagedStoragePathResolver.RequireIdentifier(update.Artifact.Id);
            if (
                update.Artifact.SchemaVersion != 1
                || !Enum.IsDefined(update.Artifact.Kind)
                || update.ExpectedRevision < 0
                || string.IsNullOrWhiteSpace(update.Artifact.Title)
            )
                throw new ValidationException(
                    "Artifact kind, title, object data and non-negative expected revision are required."
                );
            if (update.Artifact.Kind == SessionArtifactKind.Context)
            {
                if (
                    update.Artifact.Id != "context"
                    || update.Artifact.TaskId is not null
                    || update.Artifact.Context is null
                    || update.Artifact.Data is not null
                )
                    throw new ValidationException(
                        "Shared context requires ID 'context', typed context fields, no task link and no freeform data."
                    );
            }
            else if (
                update.Artifact.Id == "context"
                || update.Artifact.Context is not null
                || update.Artifact.Data?.ValueKind != JsonValueKind.Object
            )
                throw new ValidationException(
                    "Non-context artifacts require object data; ID 'context' is reserved."
                );
            if (update.Artifact.ActorId != request.ActorId)
                throw new ValidationException("Artifact actor must match the contribution actor.");
        }
        if (
            request.Updates.Select(x => x.Artifact.Id).Distinct(StringComparer.Ordinal).Count()
            != request.Updates.Count
        )
            throw new ValidationException("Duplicate artifact in update batch.");
        var files = new List<ManagedFile>
        {
            MetadataFile(request.SessionId),
            ResultFile(request.SessionId, request.OperationId),
        };
        files.AddRange(request.Updates.Select(x => ArtifactFile(request.SessionId, x.Artifact.Id)));
        var existing = await store.ReadManyAsync(files, cancellationToken);
        if (ReadResult(existing[1], requestHash) is { } prior)
            return prior;
        var metadata = ReadMetadata(request.SessionId, existing[0]);
        var now = time.GetUtcNow();
        var mutations = new List<ManagedMutation>();
        var revisions = new Dictionary<string, long>();
        for (var index = 0; index < request.Updates.Count; index++)
        {
            var update = request.Updates[index];
            var oldContent = existing[index + 2];
            var old = oldContent is null
                ? null
                : JsonSerializer.Deserialize<SharedSessionArtifact>(
                    oldContent,
                    MemoryJson.Options
                )!;
            if ((old?.Revision ?? 0) != update.ExpectedRevision)
                throw new ValidationException("Artifact revision conflict.", "revision_conflict");
            if (old is not null && old.Kind != update.Artifact.Kind)
                throw new ValidationException("An artifact's kind cannot change.");
            if (old is not null && old.TaskId != update.Artifact.TaskId)
                throw new ValidationException("An artifact's task association cannot change.");
            if (update.Artifact.TaskId is { } taskId)
            {
                if (
                    !metadata.Tasks.TryGetValue(taskId, out var ownerTask)
                    || ownerTask.OwnerId != request.ActorId
                    || ownerTask.ClaimToken is null
                    || ownerTask.ClaimToken != update.ClaimToken
                    || ownerTask.ClaimExpiresAtUtc is null
                    || ownerTask.ClaimExpiresAtUtc <= now
                    || ownerTask.Status != WorkStatus.InProgress
                )
                    throw new ValidationException(
                        "Artifact contribution requires a current task claim.",
                        "stale_claim"
                    );
            }
            update.Artifact.Revision = update.ExpectedRevision + 1;
            update.Artifact.UpdatedAtUtc = now;
            revisions.Add(update.Artifact.Id, update.Artifact.Revision);
            metadata.ArtifactRevisions[update.Artifact.Id] = update.Artifact.Revision;
            metadata.Changes.Add(
                new(
                    ++metadata.Sequence,
                    request.ActorId,
                    "artifact-updated",
                    update.Artifact.Id,
                    update.Artifact.Revision,
                    now
                )
            );
            mutations.Add(
                new(
                    files[index + 2],
                    JsonSerializer.Serialize(update.Artifact, MemoryJson.Options),
                    HashOrNull(oldContent)
                )
            );
        }
        var result = new SessionMutationResult(metadata.Sequence, revisions);
        mutations.Add(
            new(
                files[0],
                JsonSerializer.Serialize(metadata, MemoryJson.Options),
                HashOrNull(existing[0])
            )
        );
        mutations.Add(
            new(
                files[1],
                JsonSerializer.Serialize(
                    new SessionOperationResult(requestHash, result),
                    MemoryJson.Options
                ),
                null
            )
        );
        await store.CommitAsync(
            OperationKey(request.SessionId, request.OperationId),
            mutations,
            cancellationToken,
            () =>
            {
                if (
                    request.Updates.Any(update =>
                        update.Artifact.TaskId is { } id
                        && metadata.Tasks[id].ClaimExpiresAtUtc <= time.GetUtcNow()
                    )
                )
                    throw new ValidationException(
                        "Task claim expired before commit.",
                        "stale_claim"
                    );
            }
        );
        return result;
    }

    public async Task<SessionMutationResult> CoordinateAsync(
        CoordinateTaskRequest request,
        CancellationToken cancellationToken = default
    )
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await CoordinateCoreAsync(request, cancellationToken);
            }
            catch (ValidationException error)
                when (error.Code == "revision_conflict"
                    && request.Action == TaskAction.Create
                    && attempt < 3
                )
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private async Task<SessionMutationResult> CoordinateCoreAsync(
        CoordinateTaskRequest request,
        CancellationToken cancellationToken
    )
    {
        requests.Validate(request, content: true);
        ValidateIdentity(request.SessionId, request.ActorId, request.OperationId);
        ManagedStoragePathResolver.RequireIdentifier(request.TaskId);
        if (!Enum.IsDefined(request.Action) || request.ExpectedRevision < 0)
            throw new ValidationException("Invalid task action or revision.");
        var requestHash = ManagedTransactionStore.Hash(
            JsonSerializer.Serialize(request, MemoryJson.Options)
        );
        request = Clone(request);
        var files = new[]
        {
            MetadataFile(request.SessionId),
            ResultFile(request.SessionId, request.OperationId),
        };
        var existing = await store.ReadManyAsync(files, cancellationToken);
        if (ReadResult(existing[1], requestHash) is { } prior)
            return prior;
        var metadata = ReadMetadata(request.SessionId, existing[0]);
        var now = time.GetUtcNow();
        DateTimeOffset? claimValidUntil = null;
        CoordinatedTask task;
        if (request.Action == TaskAction.Create)
        {
            if (
                string.IsNullOrWhiteSpace(request.WorkKey)
                || string.IsNullOrWhiteSpace(request.Title)
                || request.ExpectedRevision != 0
            )
                throw new ValidationException(
                    "Task creation requires work key, title and expected revision zero."
                );
            var definitionHash = ManagedTransactionStore.Hash(
                JsonSerializer.Serialize(
                    new
                    {
                        request.WorkKey,
                        request.Title,
                        request.ParentStepId,
                        request.Dependencies,
                        request.AcceptanceExpectations,
                        request.NextAction,
                    },
                    MemoryJson.Options
                )
            );
            var duplicate = metadata.Tasks.Values.FirstOrDefault(x => x.WorkKey == request.WorkKey);
            if (duplicate is not null)
            {
                if (duplicate.DefinitionHash != definitionHash)
                    throw new ValidationException("Work key already describes another task.");
                var duplicateResult = new SessionMutationResult(metadata.Sequence, [], duplicate);
                await store.CommitAsync(
                    OperationKey(request.SessionId, request.OperationId),
                    [
                        new(files[0], existing[0], HashOrNull(existing[0])),
                        new(
                            files[1],
                            JsonSerializer.Serialize(
                                new SessionOperationResult(requestHash, duplicateResult),
                                MemoryJson.Options
                            ),
                            null
                        ),
                    ],
                    cancellationToken
                );
                return duplicateResult;
            }
            if (metadata.Tasks.ContainsKey(request.TaskId))
                throw new ValidationException("Task ID already exists.");
            if (request.Dependencies.Any(id => !metadata.Tasks.ContainsKey(id)))
                throw new ValidationException(
                    "Task dependencies must already exist in the same session."
                );
            if (
                request.ParentStepId is not null
                && !metadata.Tasks.ContainsKey(request.ParentStepId)
            )
                throw new ValidationException(
                    "Parent task must already exist in the same session."
                );
            task = new()
            {
                Id = request.TaskId,
                WorkKey = request.WorkKey,
                Title = request.Title,
                ParentStepId = request.ParentStepId,
                DefinitionHash = definitionHash,
                Dependencies = request.Dependencies,
                AcceptanceExpectations = request.AcceptanceExpectations,
                NextAction = request.NextAction,
            };
            metadata.Tasks.Add(task.Id, task);
        }
        else
        {
            if (!metadata.Tasks.TryGetValue(request.TaskId, out task!))
                throw new ValidationException("Task does not exist.");
            if (task.Revision != request.ExpectedRevision)
                throw new ValidationException("Task revision conflict.", "revision_conflict");
            switch (request.Action)
            {
                case TaskAction.Claim:
                    if (task.Status is WorkStatus.Completed or WorkStatus.Blocked)
                        throw new ValidationException("Task must be reopened before claiming.");
                    if (task.ClaimExpiresAtUtc > now)
                        throw new ValidationException("Task is already claimed.", "claim_conflict");
                    if (
                        task.Dependencies.Any(id =>
                            metadata.Tasks[id].Status != WorkStatus.Completed
                        )
                    )
                        throw new ValidationException("Task prerequisites are not complete.");
                    task.OwnerId = request.ActorId;
                    task.ClaimToken = Guid.NewGuid().ToString("N");
                    task.FencingGeneration++;
                    task.ClaimExpiresAtUtc = now.AddMinutes(options.Value.ClaimLeaseMinutes);
                    task.Status = WorkStatus.InProgress;
                    break;
                case TaskAction.Reopen:
                    if (
                        task.Status is not (WorkStatus.Completed or WorkStatus.Blocked)
                        || string.IsNullOrWhiteSpace(request.Reason)
                    )
                        throw new ValidationException(
                            "Reopening requires a blocked/completed task and reason."
                        );
                    task.Status = WorkStatus.Available;
                    task.Blocker = null;
                    break;
                default:
                    if (
                        task.OwnerId != request.ActorId
                        || task.ClaimToken is null
                        || task.ClaimToken != request.ClaimToken
                        || task.ClaimExpiresAtUtc is null
                        || task.ClaimExpiresAtUtc <= now
                        || task.Status != WorkStatus.InProgress
                    )
                        throw new ValidationException(
                            "Task claim is stale, expired or owned by another agent.",
                            "stale_claim"
                        );
                    claimValidUntil = task.ClaimExpiresAtUtc;
                    if (request.Action == TaskAction.Renew)
                        task.ClaimExpiresAtUtc = now.AddMinutes(options.Value.ClaimLeaseMinutes);
                    else
                    {
                        if (
                            request.Action == TaskAction.Block
                            && string.IsNullOrWhiteSpace(request.Reason)
                        )
                            throw new ValidationException("Blocking requires a reason.");
                        if (request.Action == TaskAction.Complete)
                        {
                            if (
                                request.Outputs.Count == 0
                                && string.IsNullOrWhiteSpace(request.Handoff)
                            )
                                throw new ValidationException(
                                    "Completion requires output references or a handoff."
                                );
                            if (
                                request.VerificationStatus
                                is not ("unverified" or "passed" or "failed" or "partial")
                            )
                                throw new ValidationException("Unknown verification status.");
                            if (
                                request.VerificationStatus != "unverified"
                                && request.Evidence.Count == 0
                            )
                                throw new ValidationException(
                                    "Verification claims require evidence references."
                                );
                            task.Outputs = request.Outputs;
                            task.Evidence = request.Evidence;
                            task.VerificationStatus = request.VerificationStatus;
                            task.Handoff = request.Handoff;
                        }
                        task.Status = request.Action switch
                        {
                            TaskAction.Block => WorkStatus.Blocked,
                            TaskAction.Complete => WorkStatus.Completed,
                            TaskAction.Release => WorkStatus.Available,
                            _ => throw new ValidationException("Unsupported task transition."),
                        };
                        task.Blocker = request.Action == TaskAction.Block ? request.Reason : null;
                        if (request.Action == TaskAction.Block)
                            task.Evidence = request.Evidence;
                        task.OwnerId = null;
                        task.ClaimToken = null;
                        task.ClaimExpiresAtUtc = null;
                    }
                    break;
            }
        }
        task.Revision++;
        task.UpdatedAtUtc = now;
        metadata.Changes.Add(
            new(
                ++metadata.Sequence,
                request.ActorId,
                "task-" + request.Action.ToString().ToLowerInvariant(),
                task.Id,
                task.Revision,
                now,
                request.Reason
            )
        );
        var result = new SessionMutationResult(metadata.Sequence, [], task);
        await store.CommitAsync(
            OperationKey(request.SessionId, request.OperationId),
            [
                new(
                    files[0],
                    JsonSerializer.Serialize(metadata, MemoryJson.Options),
                    HashOrNull(existing[0])
                ),
                new(
                    files[1],
                    JsonSerializer.Serialize(
                        new SessionOperationResult(requestHash, result),
                        MemoryJson.Options
                    ),
                    null
                ),
            ],
            cancellationToken,
            () =>
            {
                if (claimValidUntil <= time.GetUtcNow())
                    throw new ValidationException(
                        "Task claim expired before commit.",
                        "stale_claim"
                    );
            }
        );
        return result;
    }

    private SessionCoordinationMetadata ReadMetadata(string sessionId, string? json)
    {
        if (json is null)
            return new() { RepositoryId = paths.RepositoryId, SessionId = sessionId };
        var value = JsonSerializer.Deserialize<SessionCoordinationMetadata>(
            json,
            MemoryJson.Options
        )!;
        if (
            value.SchemaVersion != 1
            || value.SessionId != sessionId
            || value.RepositoryId != paths.RepositoryId
        )
            throw new InvalidDataException("Session metadata version or identity mismatch.");
        return value;
    }

    private static SessionMutationResult? ReadResult(string? json, string requestHash)
    {
        if (json is null)
            return null;
        var prior = JsonSerializer.Deserialize<SessionOperationResult>(json, MemoryJson.Options)!;
        if (prior.RequestHash != requestHash)
            throw new ValidationException(
                "Operation ID was already used for another request.",
                "operation_conflict"
            );
        return prior.Result;
    }

    private static string? HashOrNull(string? content) =>
        content is null ? null : ManagedTransactionStore.Hash(content);

    private static string OperationKey(string sessionId, string operationId) =>
        "session-" + ManagedTransactionStore.Hash(sessionId + "/" + operationId);

    private static T Clone<T>(T value) =>
        JsonSerializer.Deserialize<T>(
            JsonSerializer.Serialize(value, MemoryJson.Options),
            MemoryJson.Options
        )!;

    private static void ValidateIdentity(string sessionId, string actorId, string operationId)
    {
        ManagedStoragePathResolver.RequireIdentifier(sessionId);
        ManagedStoragePathResolver.RequireIdentifier(actorId);
        ManagedStoragePathResolver.RequireIdentifier(operationId);
    }
}
