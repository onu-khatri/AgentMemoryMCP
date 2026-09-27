using AgentSession.MCP.Contracts;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Services;

public sealed partial class CanonicalMemoryService
{
    public async Task<MemoryWriteResult> RecordOutcomeAsync(
        RecordOutcomeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        requests.Validate(request, content: true);
        requests.ValidateBatch(request.Evidence.Count);
        foreach (
            var id in new[]
            {
                request.MemoryId,
                request.OutcomeId,
                request.SessionId,
                request.TaskId,
                request.AgentId,
            }
        )
            ManagedStoragePathResolver.RequireIdentifier(id);
        if (
            request.ExpectedRevision < 1
            || !Enum.IsDefined(request.Result)
            || request.Evidence.Any(string.IsNullOrWhiteSpace)
            || (request.Result == OutcomeKind.Success && request.Evidence.Count == 0)
        )
            throw new ValidationException(
                "Outcome requires a positive revision, valid result and nonempty success evidence."
            );
        var requestHash = RequestHash("outcome", request);
        if (await PriorAsync(request.OutcomeId, requestHash, cancellationToken) is { } prior)
            return prior;

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var snapshot = await catalog.ReadAsync(cancellationToken);
                var record = await ForMutationAsync(
                    snapshot,
                    request.MemoryId,
                    request.ExpectedRevision,
                    cancellationToken
                );
                if (record.Tier == MemoryTier.Temp)
                    throw new ValidationException(
                        "Outcomes target durable short-term or long-term memory."
                    );
                var file = LearningCatalog.FileFor(record);
                var before = await store.ReadAsync(file, cancellationToken)
                    ?? throw new ValidationException("Outcome target disappeared.", "revision_conflict");
                var now = time.GetUtcNow();
                var outcome = new MemoryOutcome
                {
                    OutcomeId = request.OutcomeId,
                    MemoryId = record.Id,
                    RepositoryId = paths.RepositoryId,
                    SessionId = request.SessionId,
                    TaskId = request.TaskId,
                    AgentId = request.AgentId,
                    Result = request.Result,
                    Evidence = request.Evidence,
                    Notes = request.Notes,
                    CreatedAtUtc = now,
                };
                record.UseCount++;
                record.LastUsedAtUtc = now;
                switch (request.Result)
                {
                    case OutcomeKind.Success:
                        record.SuccessCount++;
                        record.LastSuccessfulAtUtc = now;
                        if (IsIndependent(record.SuccessfulConfirmations, outcome))
                            record.SuccessfulConfirmations.Add(
                                new(
                                    outcome.OutcomeId,
                                    outcome.SessionId,
                                    outcome.AgentId,
                                    outcome.Evidence.Distinct(StringComparer.Ordinal).ToList()
                                )
                            );
                        break;
                    case OutcomeKind.PartialSuccess:
                        record.PartialSuccessCount++;
                        record.LastSuccessfulAtUtc = now;
                        break;
                    case OutcomeKind.Failure:
                        record.FailureCount++;
                        record.LastFailedAtUtc = now;
                        break;
                    case OutcomeKind.Contradicted:
                        record.ContradictionCount++;
                        record.LastFailedAtUtc = now;
                        record.Contradicted = true;
                        break;
                    case OutcomeKind.Stale:
                        record.Stale = true;
                        break;
                    case OutcomeKind.NotApplicable:
                        break;
                    default:
                        throw new ValidationException("Unknown outcome result.");
                }
                record.IndependentConfirmations = record.SuccessfulConfirmations.Count;
                record.Confidence = Confidence(record);
                record.Revision++;
                record.UpdatedAtUtc = now;
                record.OperationId = request.OutcomeId;
                MemoryRecordValidator.Validate(record);
                var month = now.ToString(
                    "yyyy-MM",
                    System.Globalization.CultureInfo.InvariantCulture
                );
                var result = new MemoryWriteResult(
                    record.Id,
                    record.Revision,
                    record.Tier,
                    record.Status
                );
                await CommitAsync(
                    request.OutcomeId,
                    requestHash,
                    result,
                    snapshot,
                    [record],
                    [],
                    [
                        new(
                            file,
                            Serialize(record),
                            ManagedTransactionStore.Hash(before)
                        ),
                        new(
                            new(
                                ManagedArea.Learning,
                                null,
                                ["outcomes", month, record.Id, request.OutcomeId + ".json"]
                            ),
                            Serialize(outcome),
                            null
                        ),
                    ],
                    request.AgentId,
                    "outcome-recorded",
                    cancellationToken,
                    provenance: record
                );
                return result;
            }
            catch (ValidationException error)
                when (error.Code == "revision_conflict" && attempt < 3)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    internal static double Confidence(MemoryRecord record) =>
        (1d + record.SuccessCount + 0.5d * record.PartialSuccessCount)
        / (
            2d
            + record.SuccessCount
            + record.PartialSuccessCount
            + record.FailureCount
            + record.ContradictionCount
        );

    private static bool IsIndependent(
        IReadOnlyCollection<MemoryConfirmation> confirmations,
        MemoryOutcome outcome
    ) =>
        confirmations.All(existing =>
            existing.SessionId != outcome.SessionId
            && existing.AgentId != outcome.AgentId
            && !existing.Evidence.Intersect(outcome.Evidence, StringComparer.Ordinal).Any()
        );
}
