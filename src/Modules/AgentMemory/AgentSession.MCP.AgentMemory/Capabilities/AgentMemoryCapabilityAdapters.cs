using AgentSession.MCP.AgentMemory.Contracts.Capabilities;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Models.Memory;
using AgentSession.MCP.Services;

namespace AgentSession.MCP.AgentMemory.Capabilities;

internal sealed class MemoryQueryCapabilities(
    CanonicalMemoryService memory,
    MemoryStatusService status
) : IMemoryQueryCapabilities
{
    public Task<MemoryStatusResult> GetStatusAsync(PlatformRequestContext context, CancellationToken cancellationToken) =>
        status.GetAsync(cancellationToken);

    public Task<MemoryReadResult> GetAsync(GetMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken) =>
        memory.GetAsync(request, cancellationToken);

    public Task<RecallMemoryResult> RecallAsync(RecallMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken) =>
        memory.RecallAsync(request, cancellationToken);

    public Task<IReadOnlyList<ReviewMemoryCandidate>> ListReviewAsync(ReviewListRequest request, PlatformRequestContext context, CancellationToken cancellationToken) =>
        memory.ListReviewAsync(request, cancellationToken);
}

internal sealed class MemoryCommandCapabilities(CanonicalMemoryService memory) : IMemoryCommandCapabilities
{
    public Task<MemoryWriteResult> RememberAsync(RememberMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.RememberAsync(request, cancellationToken);
    public Task<MemoryWriteResult> UpdateAsync(UpdateMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.UpdateAsync(request, cancellationToken);
    public Task<MemoryWriteResult> DeleteAsync(DeleteMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.DeleteAsync(request, cancellationToken);
    public Task<ArchiveMemoryResult> ArchiveAsync(ArchiveMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.ArchiveAsync(request, cancellationToken);
    public Task<PreparedCompactionResult> PrepareCompactionAsync(PrepareCompactionRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.PrepareCompactionAsync(request, cancellationToken);
    public Task<CompactionResult> CommitCompactionAsync(CommitCompactionRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.CommitCompactionAsync(request, cancellationToken);
    public Task<CompactionResult> CompactAsync(CompactMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.CompactAsync(request, cancellationToken);
    public Task<MemoryEvent> RecordObservationEventAsync(RecordMemoryEventRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.RecordObservationEventAsync(request, cancellationToken);
    public Task<MemoryWriteResult> RecordOutcomeAsync(RecordOutcomeRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.RecordOutcomeAsync(request, cancellationToken);
    public Task<MemoryWriteResult> SupersedeAsync(LongTermLifecycleRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.SupersedeAsync(request, cancellationToken);
    public Task<MemoryWriteResult> RetireAsync(LongTermLifecycleRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.RetireAsync(request, cancellationToken);
    public Task<MemoryWriteResult> PromoteAsync(PromoteMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.PromoteAsync(request, cancellationToken);
    public Task<MemoryWriteResult> ReviewAsync(ReviewMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken) => memory.ReviewAsync(request, cancellationToken);
    public Task<CleanupMemoryResult> RunMaintenanceAsync(PlatformRequestContext context, CancellationToken cancellationToken) => memory.RunMaintenanceAsync(cancellationToken);
}

internal sealed class MemoryIndexCapabilities(
    MemoryReindexService reindex,
    LearningCatalog catalog
) : IMemoryIndexCapabilities
{
    public Task<ReindexMemoryResult> ReindexAsync(ReindexMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken) =>
        reindex.ReindexAsync(request, cancellationToken);

    public Task<int> RebuildIndexesAsync(PlatformRequestContext context, CancellationToken cancellationToken) =>
        catalog.RebuildAsync(cancellationToken);
}
