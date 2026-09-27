using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Services;

internal sealed record ObservationEventReceipt(string RequestHash, MemoryEvent Event);

public sealed partial class CanonicalMemoryService
{
    public async Task<MemoryEvent> RecordObservationEventAsync(
        RecordMemoryEventRequest request,
        CancellationToken cancellationToken = default
    )
    {
        requests.Validate(request, content: true);
        requests.ValidateOptionalBatch(request.Metadata?.Count ?? 0);
        ManagedStoragePathResolver.RequireIdentifier(request.EventId);
        ManagedStoragePathResolver.RequireIdentifier(request.AgentId);
        foreach (var id in new[] { request.MemoryId, request.SessionId, request.TaskId })
            if (id is not null)
                ManagedStoragePathResolver.RequireIdentifier(id);
        if (
            !request.EventType.StartsWith("observation-", StringComparison.Ordinal)
            || !NameSanitizer.IsSafePathSegment(request.EventType)
        )
            throw new ValidationException(
                "Caller-recorded event types must use the observation-* namespace.",
                "event_type_reserved"
            );
        if (
            request.Metadata?.Any(item =>
                string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)
            ) == true
        )
            throw new ValidationException("Event metadata keys and values must be non-empty.");

        var hash = RequestHash("observation-event", request);
        var receiptJson = await store.ReadAsync(OperationFile(request.EventId), cancellationToken);
        if (receiptJson is not null)
        {
            ObservationEventReceipt? receipt;
            try
            {
                receipt = JsonSerializer.Deserialize<ObservationEventReceipt>(
                    receiptJson,
                    MemoryJson.Options
                );
            }
            catch (JsonException)
            {
                throw new ValidationException(
                    "Event ID belongs to another memory action.",
                    "operation_conflict"
                );
            }
            if (receipt is null || receipt.RequestHash != hash)
                throw new ValidationException(
                    "Event ID was used for different input.",
                    "operation_conflict"
                );
            return receipt.Event;
        }

        var memoryEvent = new MemoryEvent
        {
            EventId = request.EventId,
            RepositoryId = paths.RepositoryId,
            MemoryId = request.MemoryId,
            SessionId = request.SessionId,
            TaskId = request.TaskId,
            AgentId = request.AgentId,
            TimestampUtc = time.GetUtcNow(),
            EventType = request.EventType,
            Metadata = request.Metadata ?? [],
        };
        var mutations = (await PrepareEventAppendAsync(memoryEvent, cancellationToken)).ToList();
        mutations.Add(
            new(
                OperationFile(request.EventId),
                Serialize(new ObservationEventReceipt(hash, memoryEvent)),
                null
            )
        );
        await store.CommitAsync(
            "memory-event-" + ManagedTransactionStore.Hash(request.EventId),
            mutations,
            cancellationToken
        );
        return memoryEvent;
    }
}
