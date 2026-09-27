using System.Text.Json;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Services;

internal sealed record CanonicalMemoryStatusSnapshot(
    int TempRecords,
    int ExpiredTempRecords,
    int ShortActiveRecords,
    int ShortReviewDueRecords,
    int ShortCompactedRecords,
    int ShortArchivedRecords,
    int LongCandidateRecords,
    int LongValidatedRecords,
    int PendingEmbeddingRecords,
    int InvalidCanonicalRecords,
    DateTimeOffset? LastCompactionAtUtc,
    DateTimeOffset? LastArchiveAtUtc
);

public sealed partial class CanonicalMemoryService
{
    internal async Task<CanonicalMemoryStatusSnapshot> ReadStatusSnapshotAsync(
        CancellationToken cancellationToken
    )
    {
        var snapshot = await catalog.ReadAsync(cancellationToken);
        var temp = 0;
        var expired = 0;
        var shortActive = 0;
        var reviewDue = 0;
        var compacted = 0;
        var candidates = 0;
        var validated = 0;
        var pending = 0;
        DateTimeOffset? lastCompaction = null;
        foreach (var entry in snapshot.Entries.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Tier == MemoryTier.Temp)
            {
                temp++;
                if (IsExpired(entry))
                    expired++;
                continue;
            }
            var record = await ReadCanonicalAsync(entry, cancellationToken);
            if (record.Tier == MemoryTier.Short)
            {
                if (record.Status == MemoryState.Compacted)
                {
                    compacted++;
                    if (record.Compaction?.CompactedAtUtc > lastCompaction)
                        lastCompaction = record.Compaction.CompactedAtUtc;
                }
                else if (IsOrdinarilyActive(entry))
                    shortActive++;
                if (record.ReviewAfterUtc <= time.GetUtcNow())
                    reviewDue++;
                continue;
            }
            if (record.Status == MemoryState.Candidate)
                candidates++;
            if (record.Status == MemoryState.Validated)
                validated++;
            if (
                record.EmbeddingState == EmbeddingState.PendingEmbedding
                || record.IndexState == VectorIndexState.Pending
            )
                pending++;
        }
        var archiveJson = await store.ReadAsync(ArchiveIndexFile, cancellationToken);
        var archive = archiveJson is null
            ? new ArchiveIndex(1, new(StringComparer.Ordinal))
            : JsonSerializer.Deserialize<ArchiveIndex>(archiveJson, MemoryJson.Options)
                ?? throw new InvalidDataException("Archive index is invalid.");
        return new(
            temp,
            expired,
            shortActive,
            reviewDue,
            compacted,
            archive.Records.Count,
            candidates,
            validated,
            pending,
            snapshot.Manifest.InvalidRecordIds.Count,
            lastCompaction,
            archive.Records.Values.Max(entry => entry.ArchivedAtUtc)
        );
    }
}
