using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Services;

public static class MemoryRecordValidator
{
    public static void Validate(MemoryRecord record)
    {
        ManagedStoragePathResolver.RequireIdentifier(record.Id);
        ManagedStoragePathResolver.RequireIdentifier(record.RepositoryId);
        if (record.SchemaVersion != 1)
            throw new ValidationException("Unsupported memory schema version.");
        if (
            record.Revision < 1
            || string.IsNullOrWhiteSpace(record.OperationId)
            || string.IsNullOrWhiteSpace(record.ContentHash)
            || string.IsNullOrWhiteSpace(record.Content)
        )
            throw new ValidationException(
                "Record content, hash, operation and positive revision are required."
            );
        if (
            !Enum.IsDefined(record.Tier)
            || !Enum.IsDefined(record.Status)
            || !Enum.IsDefined(record.ReviewState)
            || !Enum.IsDefined(record.EmbeddingState)
            || !Enum.IsDefined(record.IndexState)
        )
            throw new ValidationException("Unknown memory state.");
        if (
            record.CreatedAtUtc.Offset != TimeSpan.Zero
            || record.UpdatedAtUtc.Offset != TimeSpan.Zero
            || record.CreatedAtUtc == default
            || record.UpdatedAtUtc < record.CreatedAtUtc
        )
            throw new ValidationException("Record timestamps must be ordered UTC timestamps.");
        if (record.Tier == MemoryTier.Temp)
        {
            if (
                record.ExpiresAtUtc is not { } expiry
                || expiry.Offset != TimeSpan.Zero
                || expiry <= record.CreatedAtUtc
                || (expiry - record.CreatedAtUtc).TotalHours > 24
            )
                throw new ValidationException(
                    "Temporary expiry must be within 24 hours of creation."
                );
            if (
                record.Embedding is not null
                || record.EmbeddingState != EmbeddingState.NotRequired
                || record.IndexState != VectorIndexState.NotRequired
            )
                throw new ValidationException(
                    "Temporary records cannot contain embeddings or vector work."
                );
        }
        if (
            record.Tier != MemoryTier.Long
            && record.Status
                is MemoryState.Candidate
                    or MemoryState.Validated
                    or MemoryState.Retired
                    or MemoryState.Rejected
        )
            throw new ValidationException("Long-term lifecycle state requires long-term tier.");
        if (
            record.Tier == MemoryTier.Long
            && record.Status
                is not (
                    MemoryState.Candidate
                    or MemoryState.Validated
                    or MemoryState.Superseded
                    or MemoryState.Retired
                    or MemoryState.Rejected
                )
        )
            throw new ValidationException("Invalid long-term lifecycle state.");
        if (
            !double.IsFinite(record.Confidence)
            || record.Confidence is < 0 or > 1
            || record.SuccessCount < 0
            || record.FailureCount < 0
            || record.UseCount < 0
            || record.PartialSuccessCount < 0
            || record.ContradictionCount < 0
            || record.IndependentConfirmations < 0
        )
            throw new ValidationException("Invalid reliability metadata.");
        if (
            record.IndependentConfirmations != record.SuccessfulConfirmations.Count
            || record.SuccessfulConfirmations.Any(confirmation =>
                string.IsNullOrWhiteSpace(confirmation.OutcomeId)
                || string.IsNullOrWhiteSpace(confirmation.SessionId)
                || string.IsNullOrWhiteSpace(confirmation.AgentId)
                || confirmation.Evidence.Count == 0
                || confirmation.Evidence.Any(string.IsNullOrWhiteSpace)
            )
        )
            throw new ValidationException("Invalid independent confirmation metadata.");
        for (var index = 0; index < record.SuccessfulConfirmations.Count; index++)
        for (var other = index + 1; other < record.SuccessfulConfirmations.Count; other++)
        {
            var first = record.SuccessfulConfirmations[index];
            var second = record.SuccessfulConfirmations[other];
            if (
                first.OutcomeId == second.OutcomeId
                || first.SessionId == second.SessionId
                || first.AgentId == second.AgentId
                || first.Evidence.Intersect(second.Evidence, StringComparer.Ordinal).Any()
            )
                throw new ValidationException("Confirmations are not independent.");
        }
        if (
            record.Embedding is { } embedding
            && (
                embedding.Dimension <= 0
                || string.IsNullOrWhiteSpace(embedding.Model)
                || string.IsNullOrWhiteSpace(embedding.Provider)
                || string.IsNullOrWhiteSpace(embedding.EmbeddingVersion)
            )
        )
            throw new ValidationException("Invalid embedding metadata.");
        if (
            record.Compaction is { } compaction
            && (
                record.Tier != MemoryTier.Short
                || compaction.SourceMemoryIds.Count == 0
                || string.IsNullOrWhiteSpace(compaction.Summary)
                || string.IsNullOrWhiteSpace(compaction.AgentId)
                || string.IsNullOrWhiteSpace(compaction.Reason)
            )
        )
            throw new ValidationException(
                "Compaction requires short-term sources, summary, actor and reason."
            );
    }
}
