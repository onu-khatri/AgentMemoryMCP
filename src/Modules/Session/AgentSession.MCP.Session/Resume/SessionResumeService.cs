using System.Text.Json;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Core.Storage;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models;
using AgentSession.MCP.Options;
using AgentSession.MCP.Session.Contracts.Capabilities;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

internal sealed record SnapshotChunk(string Id, int Start, int Count, string Hash);

internal sealed class SessionSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public required string Id { get; set; }
    public required string RepositoryId { get; set; }
    public required string SessionId { get; set; }
    public required string ActorId { get; set; }
    public long Sequence { get; set; }
    public long AcknowledgedSequence { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public List<SnapshotChunk> Chunks { get; set; } = [];
    public int EntryCount { get; set; }
    public List<string> Unknown { get; set; } = [];
    public int DeliveredCount { get; set; }
}

/// <summary>Durable immutable snapshot content with separate delivery progress; reads never acknowledge work.</summary>
public sealed class SessionResumeService(
    IManagedTransactionStore store,
    RepositoryResource repository,
    TimeProvider time,
    IOptions<SessionCoordinationOptions> options,
    IOptions<SessionLimitsOptions> limits,
    SessionContextGuard contextGuard
) : ISessionResumeCapabilities
{
    private static ManagedFile Metadata(string session) =>
        new(ManagedArea.Session, session, ["coordination", "metadata.json"]);

    private static ManagedFile Snapshot(string session, string id) =>
        new(ManagedArea.Session, session, ["coordination", "snapshots", id + ".json"]);

    public Task<SessionResumePage> ResumeAsync(
        ResumeSessionRequest request,
        PlatformRequestContext context,
        CancellationToken cancellationToken
    )
    {
        contextGuard.RequireRepository(context);
        return ResumeAsync(
            request.SessionId,
            request.ActorId,
            request.PageSize,
            request.Cursor,
            cancellationToken
        );
    }

    public async Task<SessionResumePage> ResumeAsync(
        string sessionId,
        string actorId,
        int pageSize = 50,
        string? cursor = null,
        CancellationToken cancellationToken = default
    )
    {
        ManagedStorageIdentifier.RequireValid(sessionId);
        ManagedStorageIdentifier.RequireValid(actorId);
        if (pageSize < 1 || pageSize > limits.Value.MaxRecallResults)
            throw new ValidationException("Resume page size exceeds the configured bounds.");
        SessionSnapshot snapshot;
        string? oldSnapshot = null;
        var offset = 0;
        if (cursor is not null)
        {
            var parts = cursor.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out offset) || offset < 0)
                throw Resync();
            try
            {
                ManagedStorageIdentifier.RequireValid(parts[0]);
            }
            catch (ValidationException)
            {
                throw Resync();
            }
            oldSnapshot = await store.ReadAsync(Snapshot(sessionId, parts[0]), cancellationToken);
            snapshot = ReadSnapshot(oldSnapshot, sessionId, actorId);
            if (offset > snapshot.DeliveredCount || offset > snapshot.EntryCount)
                throw Resync();
        }
        else
        {
            var metadataJson =
                await store.ReadAsync(Metadata(sessionId), cancellationToken)
                ?? throw new ValidationException("Session does not exist.");
            var metadata = ReadMetadata(metadataJson, sessionId);
            var files = new List<ManagedFile> { Metadata(sessionId) };
            files.AddRange(
                metadata
                    .ArtifactRevisions.Keys.Order(StringComparer.Ordinal)
                    .Select(id => new ManagedFile(
                        ManagedArea.Session,
                        sessionId,
                        ["coordination", "artifacts", id + ".json"]
                    ))
            );
            var contents = await store.ReadManyAsync(files, cancellationToken);
            if (contents[0] != metadataJson)
                throw new ValidationException(
                    "Session changed while preparing resume; retry to obtain a consistent snapshot."
                );
            snapshot = new()
            {
                Id = Guid.NewGuid().ToString("N"),
                RepositoryId = repository.RepositoryId,
                SessionId = sessionId,
                ActorId = actorId,
                Sequence = metadata.Sequence,
                AcknowledgedSequence =
                    metadata.Checkpoints.GetValueOrDefault(actorId)?.AcknowledgedSequence ?? 0,
                ExpiresAtUtc = time.GetUtcNow().AddMinutes(options.Value.SnapshotLifetimeMinutes),
            };
            var kinds = new HashSet<SessionArtifactKind>();
            var snapshotEntries = new List<ResumeEntry>();
            foreach (var content in contents.Skip(1))
            {
                if (content is null)
                    throw new InvalidDataException(
                        "Session artifact index references missing content."
                    );
                var artifact = JsonSerializer.Deserialize<SharedSessionArtifact>(
                    content,
                    SessionJson.Options
                )!;
                if (
                    artifact.SchemaVersion != 1
                    || !metadata.ArtifactRevisions.TryGetValue(artifact.Id, out var revision)
                    || artifact.Revision != revision
                )
                    throw new InvalidDataException("Session artifact revision mismatch.");
                kinds.Add(artifact.Kind);
                if (artifact.Kind == SessionArtifactKind.Context)
                {
                    if (string.IsNullOrWhiteSpace(artifact.Context?.Objective))
                        snapshot.Unknown.Add("objective");
                    if (artifact.Context?.PlanReferences is null)
                        snapshot.Unknown.Add("plan-references");
                    if (artifact.Context?.Constraints is null)
                        snapshot.Unknown.Add("constraints");
                    if (artifact.Context?.NextActions is null)
                        snapshot.Unknown.Add("next-actions");
                }
                snapshotEntries.Add(
                    new("artifact", JsonSerializer.SerializeToElement(artifact, SessionJson.Options))
                );
            }
            foreach (var task in metadata.Tasks.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
                snapshotEntries.Add(
                    new("task", JsonSerializer.SerializeToElement(task, SessionJson.Options))
                );
            foreach (
                var change in metadata.Changes.Where(x =>
                    x.Sequence > snapshot.AcknowledgedSequence
                )
            )
                snapshotEntries.Add(
                    new("change", JsonSerializer.SerializeToElement(change, SessionJson.Options))
                );
            foreach (
                var kind in new[]
                {
                    SessionArtifactKind.Context,
                    SessionArtifactKind.Plan,
                    SessionArtifactKind.Decision,
                    SessionArtifactKind.Handoff,
                }
            )
                if (!kinds.Contains(kind))
                    snapshot.Unknown.Add(kind.ToString().ToLowerInvariant());
            if (metadata.Tasks.Count == 0)
                snapshot.Unknown.Add("task-progress");
            if (!kinds.Contains(SessionArtifactKind.Context))
                snapshot.Unknown.AddRange([
                    "objective",
                    "plan-references",
                    "constraints",
                    "next-actions",
                ]);
            await WriteChunksAsync(snapshot, snapshotEntries, cancellationToken);
            // Comparing metadata again at commit prevents capturing artifact content from different revisions.
            await store.CommitAsync(
                "snapshot-" + snapshot.Id,
                [
                    new(
                        Metadata(sessionId),
                        metadataJson,
                        ContentHash.Compute(metadataJson)
                    ),
                    new(
                        Snapshot(sessionId, snapshot.Id),
                        JsonSerializer.Serialize(snapshot, SessionJson.Options),
                        null
                    ),
                ],
                cancellationToken
            );
            oldSnapshot = JsonSerializer.Serialize(snapshot, SessionJson.Options);
        }
        var entries = await ReadPageAsync(snapshot, offset, pageSize, cancellationToken);
        var end = offset + entries.Length;
        if (end > snapshot.DeliveredCount)
        {
            snapshot.DeliveredCount = end;
            await store.CommitAsync(
                "delivery-" + Guid.NewGuid().ToString("N"),
                [
                    new(
                        Snapshot(sessionId, snapshot.Id),
                        JsonSerializer.Serialize(snapshot, SessionJson.Options),
                        ContentHash.Compute(oldSnapshot!)
                    ),
                ],
                cancellationToken
            );
        }
        var hasMore = end < snapshot.EntryCount;
        return new(
            snapshot.Id,
            snapshot.Sequence,
            snapshot.AcknowledgedSequence,
            entries,
            snapshot.Unknown,
            hasMore,
            hasMore ? snapshot.Id + ":" + end : null
        );
    }

    public Task<AgentCheckpoint> CheckpointAsync(
        CheckpointSessionRequest request,
        PlatformRequestContext context,
        CancellationToken cancellationToken
    )
    {
        contextGuard.RequireRepository(context);
        return CheckpointAsync(
            request.SessionId,
            request.ActorId,
            request.SnapshotId,
            request.Sequence,
            cancellationToken
        );
    }

    public async Task<AgentCheckpoint> CheckpointAsync(
        string sessionId,
        string actorId,
        string snapshotId,
        long sequence,
        CancellationToken cancellationToken = default
    )
    {
        ManagedStorageIdentifier.RequireValid(sessionId);
        ManagedStorageIdentifier.RequireValid(actorId);
        ManagedStorageIdentifier.RequireValid(snapshotId);
        var files = new[] { Metadata(sessionId), Snapshot(sessionId, snapshotId) };
        var contents = await store.ReadManyAsync(files, cancellationToken);
        var snapshot = ReadSnapshot(contents[1], sessionId, actorId);
        if (snapshot.Sequence != sequence || snapshot.DeliveredCount != snapshot.EntryCount)
            throw new ValidationException(
                "Checkpoint requires a fully consumed snapshot and its exact sequence."
            );
        var metadata = ReadMetadata(contents[0] ?? throw Resync(), sessionId);
        if (sequence > metadata.Sequence)
            throw Resync();
        var previous = metadata.Checkpoints.GetValueOrDefault(actorId);
        if (previous is not null && sequence < previous.AcknowledgedSequence)
            throw new ValidationException("Checkpoint cannot move backwards.");
        if (previous?.AcknowledgedSequence == sequence)
            return previous;
        var checkpoint = new AgentCheckpoint(actorId, sequence, time.GetUtcNow());
        metadata.Checkpoints[actorId] = checkpoint;
        await store.CommitAsync(
            "checkpoint-" + Guid.NewGuid().ToString("N"),
            [
                new(
                    files[0],
                    JsonSerializer.Serialize(metadata, SessionJson.Options),
                    ContentHash.Compute(contents[0]!)
                ),
                new(files[1], contents[1], ContentHash.Compute(contents[1]!)),
            ],
            cancellationToken,
            () =>
            {
                if (snapshot.ExpiresAtUtc <= time.GetUtcNow())
                    throw Resync();
            }
        );
        return checkpoint;
    }

    private async Task WriteChunksAsync(
        SessionSnapshot snapshot,
        List<ResumeEntry> entries,
        CancellationToken cancellationToken
    )
    {
        snapshot.EntryCount = entries.Count;
        var start = 0;
        while (start < entries.Count)
        {
            var count = Math.Min(50, entries.Count - start);
            var json = JsonSerializer.Serialize(entries.GetRange(start, count), SessionJson.Options);
            while (
                System.Text.Encoding.UTF8.GetByteCount(json) > limits.Value.MaxRecordBytes
            )
            {
                if (count == 1)
                    throw new ValidationException(
                        "One resume entry exceeds the configured record limit."
                    );
                count = Math.Max(1, count / 2);
                json = JsonSerializer.Serialize(entries.GetRange(start, count), SessionJson.Options);
            }
            var id = snapshot.Id + "-" + snapshot.Chunks.Count;
            await store.CommitAsync(
                "snapshot-chunk-" + id,
                [new(Snapshot(snapshot.SessionId, id), json, null)],
                cancellationToken
            );
            snapshot.Chunks.Add(new(id, start, count, ContentHash.Compute(json)));
            start += count;
        }
        // Only the final manifest is a usable cursor. Interrupted chunk writes cannot expose an
        // incomplete snapshot; maintenance may remove expired manifests and orphaned chunks.
    }

    private async Task<ResumeEntry[]> ReadPageAsync(
        SessionSnapshot snapshot,
        int offset,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        var chunks = snapshot
            .Chunks.Where(chunk =>
                chunk.Start < (long)offset + pageSize && chunk.Start + chunk.Count > offset
            )
            .ToArray();
        var contents = await store.ReadManyAsync(
            chunks.Select(chunk => Snapshot(snapshot.SessionId, chunk.Id)).ToArray(),
            cancellationToken
        );
        var result = new List<ResumeEntry>();
        var bytes = 0;
        for (var i = 0; i < chunks.Length; i++)
        {
            var content = contents[i];
            if (content is null || ContentHash.Compute(content) != chunks[i].Hash)
                throw Resync();
            var values = JsonSerializer.Deserialize<List<ResumeEntry>>(
                content,
                SessionJson.Options
            )!;
            if (values.Count != chunks[i].Count)
                throw Resync();
            foreach (var entry in values.Skip(Math.Max(0, offset - chunks[i].Start)))
            {
                var entryBytes = System.Text.Encoding.UTF8.GetByteCount(
                    JsonSerializer.Serialize(entry, SessionJson.Options)
                );
                if (result.Count > 0 && bytes + entryBytes > limits.Value.MaxRecordBytes / 2)
                    return result.ToArray();
                result.Add(entry);
                bytes += entryBytes;
                if (result.Count == pageSize)
                    return result.ToArray();
            }
        }
        return result.ToArray();
    }

    private SessionSnapshot ReadSnapshot(string? json, string session, string actor)
    {
        if (json is null)
            throw Resync();
        var value = JsonSerializer.Deserialize<SessionSnapshot>(json, SessionJson.Options)!;
        if (
            value.SchemaVersion != 1
            || value.RepositoryId != repository.RepositoryId
            || value.SessionId != session
            || value.ActorId != actor
            || value.ExpiresAtUtc <= time.GetUtcNow()
        )
            throw Resync();
        return value;
    }

    private SessionCoordinationMetadata ReadMetadata(string json, string session)
    {
        var value = JsonSerializer.Deserialize<SessionCoordinationMetadata>(
            json,
            SessionJson.Options
        )!;
        if (
            value.SchemaVersion != 1
            || value.RepositoryId != repository.RepositoryId
            || value.SessionId != session
        )
            throw new InvalidDataException("Session metadata version or identity mismatch.");
        return value;
    }

    private static ValidationException Resync() =>
        new("Invalid or expired resume cursor; start a full resynchronization without a cursor.");
}
