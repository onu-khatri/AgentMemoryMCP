# Architecture and mental model

[Wiki index](README.md)

AgentMemoryMCP is a local, repository-scoped continuity system. It is not a replacement for source control, a task tracker, test evidence or user authority. It supplies structured persisted state that an agent can retrieve on demand instead of carrying every earlier message in the active prompt.

## Four layers of state

| Layer | Examples | Primary tools | Lifetime |
| --- | --- | --- | --- |
| Live model context | Current request, files being edited, immediate reasoning | Host/model context | One interaction or context window |
| Shared session state | Objective, plans, decisions, findings, outputs, handoffs and coordinated tasks | Session tools | Until the session is intentionally removed outside these tools |
| Reusable learning | Facts, patterns, outcomes, review decisions and validated lessons | `memory_*` tools | Tier and lifecycle dependent |
| Derived retrieval state | Filesystem indexes, embeddings and Qdrant points | Maintenance/reindex tools | Rebuildable from canonical records |

Canonical session and learning files are the recoverable source. Qdrant vectors and filesystem indexes are derived. Removing or migrating the vector database must not remove canonical learning.

## Authority order

Custom agents should resolve conflicts in this order:

1. Explicit current user decision or approval.
2. Current repository instructions and policies.
3. Current source, configuration, tests and external system evidence.
4. Current shared-session state with provenance.
5. Recalled reusable memory.

The lower layer can suggest what to inspect, but it cannot silently override a higher layer.

## Repository and session boundaries

The server process binds one stable `Repository__Id`. Request payloads cannot switch repositories. Sessions are isolated by that repository and a filesystem-safe session ID. Worktrees share data only when configured with the same repository ID intentionally.

Use a session for one coherent body of work, such as `payments-migration` or `incident-2026-09-27`. Use tasks and plan artifacts inside the session rather than creating a session per subtask.

## What belongs where

| Information | Store | Reason |
| --- | --- | --- |
| “Agent B owns webhook tests until 15:30 UTC” | Coordinated task | Ownership and lease semantics are enforced there. |
| “Current plan is plan-v3” | Shared context `planReferences` | All agents resolve the same active plan explicitly. |
| “The webhook fixture fails when timestamps lack `Z`” | Finding artifact | It is operational evidence for this session. |
| “All API timestamps in this repository must be UTC with `Z`” | Short-term learning after verification | It can help future sessions. |
| “Try renaming this local variable next” | Live context or `temp` if restart continuity is needed | It is transient. |
| Full conversation transcript or private reasoning | Nowhere | It increases noise and may expose sensitive reasoning. |

## Real-life example: remembered convention conflicts with code

Semantic recall returns a validated memory saying the service uses Newtonsoft.Json. The current project file and source use `System.Text.Json`, and a recent decision artifact records the migration. The agent treats the recalled record as stale evidence, follows the current code, records a `contradicted` or `stale` outcome against the memory, and later retires or supersedes it through the explicit lifecycle. It never changes the code merely because the memory had a high similarity score.

## Practice for agent authors

Put the authority order and the two storage classes directly in the agent's instructions. An agent that only says “save memory regularly” will usually over-save transcripts or put task state in reusable memory. Name the tools and boundaries explicitly.

