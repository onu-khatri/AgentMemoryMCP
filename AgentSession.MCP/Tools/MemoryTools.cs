using System.ComponentModel;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;
using AgentSession.MCP.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace AgentSession.MCP.Tools;

public sealed class MemoryTools(
    CanonicalMemoryService memory,
    LearningCatalog catalog,
    MemoryStatusService status,
    MemoryReindexService reindex
)
{
    [McpServerTool(UseStructuredContent = true, ReadOnly = true, OpenWorld = false)]
    [Description(
        "Use to diagnose repository-scoped memory counts, roots, recovery backlog, Ollama/Qdrant/model/collection health and maintenance activity. It returns no memory content. Do not use it to recover context or learning; use resume_agent_session or memory_recall."
    )]
    public Task<MemoryStatusResult> memory_status(CancellationToken cancellationToken) =>
        Invoke(() => status.GetAsync(cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use to persist concise reusable facts, evidence and successful or failed patterns as temp, short or long_candidate memory across sessions. Use append_agent_memory for session-specific progress, plans, decisions and handoffs. Do not store transcripts, hidden reasoning or secrets. Temp expires within 24h; long_candidate is unvalidated. Retry with the same operationId."
    )]
    public Task<MemoryWriteResult> memory_remember(
        RememberMemoryRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.RememberAsync(request, cancellationToken));

    [McpServerTool(UseStructuredContent = true, Destructive = false, OpenWorld = false)]
    [Description(
        "Use to read one exact canonical advisory record by memoryId, usually after memory_recall or when an ID is already known. Historical long records require includeHistory; archived short records require includeArchived. Do not use it to search or resume session work. Expired temp is never returned."
    )]
    public Task<MemoryReadResult> memory_get(
        GetMemoryRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.GetAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use only to replace an existing temporary record with expectedRevision and operationId while preserving creation time; expiry can only shorten. Do not use it to rewrite short or long-term learning. Use review, compaction, promotion, supersession or retirement for durable lifecycle changes."
    )]
    public Task<MemoryWriteResult> memory_update(
        UpdateMemoryRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.UpdateAsync(request, cancellationToken));

    [McpServerTool(UseStructuredContent = true, Destructive = false, OpenWorld = false)]
    [Description(
        "Use as the primary persisted reusable-memory search before repeating research or reconstructing known facts. Returns bounded advisory records in temp, short and long order with filters; an empty query means recent. Long-term search uses repository Qdrant and degrades to labeled lexical matches during outages. Do not treat recalled memory as authority over current code, tests, instructions or user decisions. Candidates and history require explicit flags."
    )]
    public Task<RecallMemoryResult> memory_recall(
        RecallMemoryRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.RecallAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use for intentional physical deletion of a temp or short record with actor, reason and expected revision. Protected removal requires a matching operator-managed grant; MCP tools cannot create grants. Do not delete long-term learning or treat delete_candidate review as deletion; retire long-term records instead."
    )]
    public Task<MemoryWriteResult> memory_delete(
        DeleteMemoryRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.DeleteAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use to move selected revision-controlled short-term records into a verified bounded JSONL+gzip archive before removing active copies. Protected records require operator-managed archive grants. Do not use review marking as a substitute for archival, and do not archive records merely because they are old."
    )]
    public Task<ArchiveMemoryResult> memory_archive(
        ArchiveMemoryRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.ArchiveAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use as the first step when an agent needs to inspect reviewed short-term sources before writing a compacted representation. Returns unchanged sources and a cryptographic revision token. It does not modify or archive sources. Do not use memory_compact when this separate review step is required."
    )]
    public Task<PreparedCompactionResult> memory_prepare_compaction(
        PrepareCompactionRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.PrepareCompactionAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use after memory_prepare_compaction to commit the agent-reviewed representation. Validates the preparation token, source revisions, complete evidence and contradiction coverage, and policy grants; originals enter a verified archive in the same recoverable commit. Do not use without the matching preparation result or with an agent-invented token."
    )]
    public Task<CompactionResult> memory_commit_compaction(
        CommitCompactionRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.CommitCompactionAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use as the one-call compaction path only when the agent has already reviewed all sources and prepared a complete summary and source coverage. The server checks structure and performs verified archival; it does not generate or validate semantic truth. Do not use when the agent must first inspect a stable prepared snapshot; use prepare then commit."
    )]
    public Task<CompactionResult> memory_compact(
        CompactMemoryRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.CompactAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use to append a replay-safe metadata-only observation such as an external milestone or diagnostic signal. Caller event types must use observation-*. Do not record outcomes, content, task transitions or authoritative memory lifecycle events here; use their dedicated tools."
    )]
    public Task<MemoryEvent> memory_record_event(
        RecordMemoryEventRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.RecordObservationEventAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use to attach one replay-safe success, failure or other supported outcome to durable memory so reliability and validation policy can use real experience. Independent successes require distinct session, agent and evidence references. Do not use it for arbitrary logs or task completion. Confidence remains advisory."
    )]
    public Task<MemoryWriteResult> memory_record_outcome(
        RecordOutcomeRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.RecordOutcomeAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use when active long-term learning has a known canonical replacement. Supersedes it while preserving bidirectional history and requires source revision, actor, reason and evidence. Do not use for simple removal without a replacement; use memory_retire."
    )]
    public Task<MemoryWriteResult> memory_supersede(
        LongTermLifecycleRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.SupersedeAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use when active long-term learning should leave ordinary recall without physical deletion. Requires source revision, actor, reason and evidence; an optional replacement link is preserved. Do not use when a canonical replacement should become the active successor; use memory_supersede."
    )]
    public Task<MemoryWriteResult> memory_retire(
        LongTermLifecycleRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.RetireAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use to advance temp to short, short to long_candidate, or an eligible long candidate to validated status while preserving provenance and replay identity. Candidate validation requires configured independent confirmations or authoritative evidence and any operator grant. Do not copy session artifacts into learning or assume promotion makes advisory memory authoritative."
    )]
    public Task<MemoryWriteResult> memory_promote(
        PromoteMemoryRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.PromoteAsync(request, cancellationToken));

    [McpServerTool(UseStructuredContent = true, ReadOnly = true, OpenWorld = false)]
    [Description(
        "Use to inspect a bounded set of short-term records due for review or matching state, age and metadata filters. This is read-only and age is only a suggestion. Do not infer deletion, archival or promotion from listing or age alone."
    )]
    public Task<IReadOnlyList<ReviewMemoryCandidate>> memory_list_review(
        ReviewListRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.ListReviewAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use to persist an intentional short-term review decision: keep, compact_candidate, promotion_candidate, archive_candidate or delete_candidate, with actor, reason, evidence and revision. This records review state only. Do not treat it as compaction, promotion, archival or deletion; call the matching lifecycle tool separately."
    )]
    public Task<MemoryWriteResult> memory_mark_reviewed(
        ReviewMemoryRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => memory.ReviewAsync(request, cancellationToken));

    [McpServerTool(UseStructuredContent = true, Destructive = true, OpenWorld = false)]
    [Description(
        "Use for bounded maintenance of temp expiry and filesystem catalog repair. Expired reads remain blocked while cleanup is pending. Do not use it to recall memory, remove durable session progress or make semantic curation decisions."
    )]
    public Task<CleanupMemoryResult> memory_cleanup(CancellationToken cancellationToken) =>
        Invoke(() => memory.RunMaintenanceAsync(cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use for bounded repository-scoped filesystem repair, pending-vector work, selected-ID indexing, Qdrant rebuild or controlled embedding-model migration. Canonical files remain the recoverable source and prior vector collections are retained. Do not use memory_rebuild_indexes when vectors or model migration are required."
    )]
    public Task<ReindexMemoryResult> memory_reindex(
        ReindexMemoryRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => reindex.ReindexAsync(request, cancellationToken));

    [McpServerTool(UseStructuredContent = true, Destructive = false, OpenWorld = false)]
    [Description(
        "Use only to rebuild derived filesystem indexes from canonical records in the bound repository. Unknown or corrupt records remain untouched and are reported. Do not use it to rebuild vectors or migrate embeddings; use memory_reindex for those operations."
    )]
    public Task<int> memory_rebuild_indexes(CancellationToken cancellationToken) =>
        Invoke(() => catalog.RebuildAsync(cancellationToken));

    private static async Task<T> Invoke<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ValidationException error)
        {
            throw new McpException(error.Code + ": " + error.Message);
        }
        catch (TimeoutException)
        {
            throw new McpException(
                "lock_timeout: Repository is busy; retry the same operation ID."
            );
        }
        catch (InvalidDataException)
        {
            throw new McpException("storage_invalid: Canonical data requires operator inspection.");
        }
    }
}
