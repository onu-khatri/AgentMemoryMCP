using System.ComponentModel;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models;
using AgentSession.MCP.Observability;
using AgentSession.MCP.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using OnuObservability.Mcp;

namespace AgentSession.MCP.Tools;

public sealed class SharedSessionTools(
    SessionCoordinationService coordination,
    SessionResumeService resume,
    SharedSessionLifecycleService lifecycle,
    IMcpTelemetryOutcomeContext telemetryOutcomes
)
{
    [McpServerTool(
        UseStructuredContent = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use when a stable sessionId has been selected to create or activate repository-scoped shared work. This returns session identity only; it does not load prior context. Immediately call resume_agent_session before planning or repeating work. Do not create a new sessionId when continuing known work."
    )]
    public Task<SharedSessionActivation> create_or_activate_session(
        ActivateSharedSessionRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => lifecycle.ActivateAsync(request, cancellationToken));

    [McpServerTool(UseStructuredContent = true, ReadOnly = true, OpenWorld = false)]
    [Description(
        "Use when the correct existing sessionId is unknown. Returns a bounded repository-scoped page of session identities; continue with after while hasMore is true. It does not return session content. Do not use it to resume work; select an ID, activate it, then call resume_agent_session."
    )]
    public Task<SharedSessionList> list_agent_sessions(
        ListSharedSessionsRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => lifecycle.ListAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use to save durable session-specific context, decisions, findings, handoffs, plans or outputs so other agents can resume without repeating work. Atomically applies structured updates with actorId, operationId and expected revisions; task-linked artifacts require the current claimToken. Use memory_remember instead for reusable learning across sessions. Do not encode task state transitions here; use coordinate_agent_task. Obsolete freeform payloads are unsupported."
    )]
    public Task<SessionMutationResult> append_agent_memory(
        UpdateSessionArtifactsRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => coordination.UpdateArtifactsAsync(request, cancellationToken));

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use as the authoritative session task-state path to create, claim, renew, release, block, complete or explicitly reopen shared work. Mutations require expectedRevision and operationId; owner operations require the current claimToken from a server-time lease. Completion records outputs, evidence and handoff, and never infers that unverified work passed. Do not represent task ownership or transitions only in append_agent_memory. Claims do not fence external side effects."
    )]
    public Task<SessionMutationResult> coordinate_agent_task(
        CoordinateTaskRequest request,
        CancellationToken cancellationToken
    ) => Invoke(() => coordination.CoordinateAsync(request, cancellationToken));

    [McpServerTool(UseStructuredContent = true, Destructive = false, OpenWorld = false)]
    [Description(
        "Use as the primary persisted session-memory read before planning, continuing, delegating or repeating work. Returns a consistent snapshot of context, artifacts, tasks and changes since this agent's checkpoint. Follow cursor until hasMore is false; reads do not acknowledge work. Do not checkpoint until every page is consumed and used. Invalid or expired cursors require a full resync; external references are not read."
    )]
    public Task<SessionResumePage> resume_agent_session(
        ResumeSessionRequest request,
        CancellationToken cancellationToken
    ) =>
        Invoke(() =>
            resume.ResumeAsync(
                request.SessionId,
                request.ActorId,
                request.PageSize,
                request.Cursor,
                cancellationToken
            )
        );

    [McpServerTool(
        UseStructuredContent = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Use only after this agent has fully consumed every page of a resume_agent_session snapshot. Acknowledges that snapshot's exact sequence so later resumes return newer changes. It does not save context, outputs or a handoff; persist those first with append_agent_memory. Do not acknowledge another agent, partial delivery, future work or an older sequence."
    )]
    public Task<AgentCheckpoint> checkpoint_agent_session(
        CheckpointSessionRequest request,
        CancellationToken cancellationToken
    ) =>
        Invoke(() =>
            resume.CheckpointAsync(
                request.SessionId,
                request.ActorId,
                request.SnapshotId,
                request.Sequence,
                cancellationToken
            )
        );

    private async Task<T> Invoke<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ValidationException error)
        {
            throw McpClassifiedException.Create(
                AgentMemoryMcpTelemetryOutcomeClassifier.Classify(error).Error?.Value
                    + ": " + error.Message,
                AgentMemoryMcpTelemetryOutcomeClassifier.Classify(error),
                telemetryOutcomes);
        }
        catch (TimeoutException)
        {
            throw McpClassifiedException.Create(
                "lock_timeout: Repository is busy; retry the same operation ID.",
                new(OnuObservability.Model.CommonOutcomes.DeadlineExceeded, new("lock_timeout")),
                telemetryOutcomes);
        }
        catch (InvalidDataException)
        {
            throw McpClassifiedException.Create(
                "storage_invalid: Managed data requires operator inspection.",
                new(McpTelemetrySchema.ToolErrorOutcome, new("storage_invalid")),
                telemetryOutcomes);
        }
    }
}
