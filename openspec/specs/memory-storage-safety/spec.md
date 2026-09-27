## Purpose

Keep centrally stored memory isolated by repository and safe across concurrent calls, crashes, and sensitive input.

## Requirements
### Requirement: Central storage with repository isolation
The default system root SHALL be `%USERPROFILE%/.codex/AgentMemory`. Sessions SHALL reside under `sessions/<repository-id>/`; learning SHALL reside under `repositories/<repository-id>/AiLearning/`. Each server instance SHALL bind one stable repository identity and reject mismatching request identities. All managed runtime writes SHALL stay inside the configured central root and the appropriate bound repository subtree, except shared vector/lock infrastructure. Repository relocation SHALL NOT change configured identity.

#### Scenario: Same IDs in different repositories
- **WHEN** two repository instances use the same session or memory identifier
- **THEN** their files, recall, mutations and maintenance remain isolated

#### Scenario: Path escape
- **WHEN** an identifier, manifest, link, junction or configuration would escape its permitted subtree
- **THEN** the operation fails without writing outside that subtree

### Requirement: Versioned canonical files and rebuildable indexes
Canonical records SHALL be inspectable UTF-8 versioned files validated before commit. Indexes SHALL be rebuildable from records and verified archives. Unknown schema versions or corrupt records SHALL be reported without destructive reinterpretation. Reads SHALL validate canonical lifecycle/revision rather than trust stale indexes.

#### Scenario: Corrupt index
- **WHEN** a derived index is missing or malformed
- **THEN** the server repairs it from canonical data without losing records and reports index health

### Requirement: Safe concurrent and recoverable mutations
Mutations SHALL use atomic file replacement, durable operation identity, revision checks and coordination across processes sharing storage. Concurrent identical creates SHALL not produce uncontrolled duplicates. Interrupted multi-file changes SHALL recover without lost source content, duplicate outcomes or partially visible lifecycle transitions. A reused idempotency key with a different payload SHALL fail.

#### Scenario: Two server processes update one record
- **WHEN** both submit an update based on the same revision
- **THEN** one succeeds and the other observes a conflict or a documented retry without lost updates

#### Scenario: Crash between canonical and index updates
- **WHEN** the process stops after canonical commit before derived updates complete
- **THEN** restart repairs pending work and reports the committed operation consistently

### Requirement: Sensitive-data policy covers every sink
Before persistence or embedding the server SHALL reject recognized secrets by default or apply explicitly configured redaction. Policy SHALL cover content, nested metadata, evidence, legacy tools, migration, archives, journals and embedding requests. Errors and logs SHALL NOT echo rejected content. Hidden reasoning SHALL NOT be requested or stored as an operational artifact.

#### Scenario: Secret in nested evidence
- **WHEN** a request contains a recognized token in structured evidence
- **THEN** it is rejected before staging, files, logs, journal, vector payload or embedding request contains that token

### Requirement: Auditable bounded operations
Lifecycle events SHALL carry event ID, memory ID, actor/session/task, UTC timestamp, event type and sanitized metadata. Replay SHALL not duplicate logical events. Caller-recorded observations SHALL NOT masquerade as committed lifecycle transitions. Inputs, result counts, archive expansion, retries and background work SHALL be bounded; capacity exhaustion SHALL report errors instead of silently deleting knowledge.

#### Scenario: Oversized request
- **WHEN** content or nested payload exceeds configured limits
- **THEN** the tool returns a validation error with no partial persistence

### Requirement: Session coordination commits remain consistent
Structured session updates, task ownership and checkpoints SHALL use revision checks, durable replay identity and recoverable commit boundaries. Expired or superseded claim tokens SHALL NOT mutate task-owned state. Learning retention cleanup SHALL NOT remove durable session coordination artifacts. Generic artifact writes SHALL NOT bypass reserved coordination paths or ownership checks.

#### Scenario: Partial multi-artifact write
- **WHEN** a process fails during a structured contribution touching findings and handoff artifacts
- **THEN** recovery exposes the complete committed contribution or the prior state, never a falsely completed partial contribution

#### Scenario: Late completion by previous owner
- **WHEN** a task is reclaimed after lease expiry and its former owner submits completion
- **THEN** the stale token is rejected and the current owner's work remains unchanged
