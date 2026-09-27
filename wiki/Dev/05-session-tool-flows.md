# Session tool end-to-end flows

[Previous: Storage](04-storage-concurrency-and-transactions.md) | [Developer wiki](README.md) | [Next: Core memory tools](06-core-memory-tool-flows.md)

The six tools in `SharedSessionTools` catch domain validation, timeout and invalid-storage errors and expose stable MCP errors. They delegate all behavior to session services.

## `create_or_activate_session`

```mermaid
flowchart LR
    A[MCP request] --> B[SharedSessionTools.create_or_activate_session]
    B --> C[SharedSessionLifecycleService.ActivateAsync]
    C --> D[Validate session, actor, operation IDs]
    D --> E[Read metadata and operation receipt]
    E --> F{Session exists?}
    F -->|No| G[Create metadata + receipt in one transaction]
    F -->|Yes, same request| H[Return existing activation]
    F -->|Conflicting operation| I[operation_conflict]
    G --> J[SharedSessionActivation]
    H --> J
```

Activation returns identity and current sequence. It does not load artifacts.

## `list_agent_sessions`

```mermaid
flowchart LR
    A[MCP request] --> B[SharedSessionTools.list_agent_sessions]
    B --> C[SharedSessionLifecycleService.ListAsync]
    C --> D[Validate pageSize and after]
    D --> E[ManagedTransactionStore.ListSessionIdsAsync]
    E --> F[Lock + recover pending intents]
    F --> G[Enumerate safe session directories with metadata]
    G --> H[Sort, bound, calculate hasMore/after]
    H --> I[SharedSessionList]
```

Only IDs are returned; clients resume a selected session separately.

## `append_agent_memory`

```mermaid
flowchart TD
    A[MCP request] --> B[SharedSessionTools.append_agent_memory]
    B --> C[SessionCoordinationService.UpdateArtifactsAsync]
    C --> D[Validate batch, IDs, artifact kinds and content policy]
    D --> E[Read metadata, artifacts and operation receipt]
    E --> F{Replay receipt?}
    F -->|same request| G[Return prior SessionMutationResult]
    F -->|different| H[operation_conflict]
    F -->|no| I[Check expected revisions]
    I --> J{Task-linked artifact?}
    J -->|yes| K[Verify owner, live claim token and in-progress task]
    J -->|no| L[Validate context/data shape]
    K --> L
    L --> M[Increment sequence and artifact revisions]
    M --> N[Commit artifacts + metadata + receipt atomically]
    N --> O[SessionMutationResult]
```

The service reserves task state from generic artifact writes. Artifact updates replace the complete representation.

## `coordinate_agent_task`

```mermaid
flowchart TD
    A[MCP request] --> B[SharedSessionTools.coordinate_agent_task]
    B --> C[SessionCoordinationService.CoordinateAsync]
    C --> D[Validate request and idempotency]
    D --> E{Action}
    E -->|create| F[Validate workKey, title, dependencies and duplicate definition]
    E -->|claim| G[Check revision, availability, lease expiry and prerequisites]
    E -->|reopen| H[Require completed/blocked state and reason]
    E -->|renew/release/block/complete| I[Require owner + live claimToken]
    F --> J[Apply task state]
    G --> J
    H --> J
    I --> J
    J --> K[Record outputs/evidence/handoff where required]
    K --> L[Increment task revision and session sequence]
    L --> M[Commit metadata + receipt]
    M --> N[SessionMutationResult]
```

Claims use server time, expiry and fencing generation. Completion evidence is caller-reported; the server does not run the verification.

## `resume_agent_session`

```mermaid
flowchart TD
    A[MCP request] --> B[SharedSessionTools.resume_agent_session]
    B --> C[SessionResumeService.ResumeAsync]
    C --> D[Validate session, actor, page size and cursor]
    D --> E{Cursor supplied?}
    E -->|No| F[Read metadata, artifacts, tasks and actor checkpoint]
    F --> G[Build immutable ordered ResumeEntry snapshot]
    G --> H[Chunk by count and byte limits]
    H --> I[Persist snapshot manifest and chunks]
    E -->|Yes| J[Load and validate actor-owned unexpired snapshot]
    I --> K[Read requested chunk and verify hash]
    J --> K
    K --> L[Return page, cursor, hasMore, sequence and unknown fields]
```

Later concurrent commits are excluded from the current immutable snapshot and appear in the next resume.

## `checkpoint_agent_session`

```mermaid
flowchart TD
    A[MCP request] --> B[SharedSessionTools.checkpoint_agent_session]
    B --> C[SessionResumeService.CheckpointAsync]
    C --> D[Load snapshot manifest and metadata]
    D --> E{Actor, sequence, expiry and all-page delivery valid?}
    E -->|No| F[Reject partial, foreign, expired, future or backward checkpoint]
    E -->|Yes| G[Update actor checkpoint]
    G --> H[Commit metadata with the expected metadata and snapshot hashes]
    H --> I[Verify the snapshot remains unexpired during commit]
    I --> J[AgentCheckpoint]
```

Checkpointing acknowledges consumption; it saves no work content.

## Session data files

`SessionCoordinationService` and `SessionResumeService` use managed files below the session's `coordination` directory for metadata, artifacts, operation receipts, snapshot manifests and chunks. Exact names are server-owned and must not be constructed by clients.
