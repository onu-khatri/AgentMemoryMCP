using AgentSession.MCP.Contracts;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.AgentMemory.Contracts.Capabilities;

public interface IMemoryCommandCapabilities
{
    Task<MemoryWriteResult> RememberAsync(RememberMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<MemoryWriteResult> UpdateAsync(UpdateMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<MemoryWriteResult> DeleteAsync(DeleteMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<ArchiveMemoryResult> ArchiveAsync(ArchiveMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<PreparedCompactionResult> PrepareCompactionAsync(PrepareCompactionRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<CompactionResult> CommitCompactionAsync(CommitCompactionRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<CompactionResult> CompactAsync(CompactMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<MemoryEvent> RecordObservationEventAsync(RecordMemoryEventRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<MemoryWriteResult> RecordOutcomeAsync(RecordOutcomeRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<MemoryWriteResult> SupersedeAsync(LongTermLifecycleRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<MemoryWriteResult> RetireAsync(LongTermLifecycleRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<MemoryWriteResult> PromoteAsync(PromoteMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<MemoryWriteResult> ReviewAsync(ReviewMemoryRequest request, PlatformRequestContext context, CancellationToken cancellationToken);
    Task<CleanupMemoryResult> RunMaintenanceAsync(PlatformRequestContext context, CancellationToken cancellationToken);
}
