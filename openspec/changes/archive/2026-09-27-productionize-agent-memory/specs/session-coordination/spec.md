## Purpose

Enable agents to resume interrupted work and coordinate in one shared session using durable context, contributions and task ownership.

## ADDED Requirements

### Requirement: Complete restart-safe session resumption
Resume SHALL return the persisted objective, current plan references, decisions/constraints, completed tasks with outputs and verification evidence, active tasks with owners and expiry, blockers/dependencies, handoffs and next actions. Missing information SHALL be explicitly unknown. Resumption SHALL work after process restart without embedding or vector services and SHALL NOT require agents to repeat recorded completed work merely to reconstruct context.

#### Scenario: Agent returns after stopping
- **WHEN** an agent resumes a session after its process exited
- **THEN** it receives the saved working context, completed outputs, unfinished work and next actions with provenance and current task ownership

### Requirement: Shared structured artifact contributions

The session SHALL have at most one shared context artifact, with reserved ID `context`, typed objective, plan references, constraints and next actions. Updating it SHALL require the current revision; callers SHALL reconcile conflicts explicitly. Null fields SHALL be reported as unknown, while an explicitly empty list SHALL mean no entries were recorded.
append_agent_memory SHALL support atomic structured updates to context, decisions, findings and handoff artifacts with actor, stable record IDs, session/task references, evidence, expected revisions and operation ID. Other agents in the same session SHALL see committed contributions. Conflicting revisions SHALL fail without lost updates. Task state transitions SHALL require the task coordination operation rather than unrestricted artifact writes.

#### Scenario: Two agents update the same finding
- **WHEN** agents submit different replacements based on one revision
- **THEN** one commits and the other receives a conflict with no silent overwrite

#### Scenario: Retry a multi-artifact contribution
- **WHEN** an identical operation is retried after a lost response
- **THEN** the server returns its original committed result without duplicate contributions

### Requirement: Structured updates define the supported contract
append_agent_memory SHALL accept explicit structured contributions and return committed revisions and change-sequence metadata. Unsupported legacy freeform payloads SHALL fail clearly without fabricating task completion, ownership or approval. Historical data SHALL not be modified to satisfy new contracts.

#### Scenario: Unsupported text-only request
- **WHEN** a caller submits an obsolete text-only payload without a deliberately supported adapter
- **THEN** validation returns guidance for structured updates and performs no mutation

### Requirement: Atomic exclusive task claims
Tasks SHALL have stable session-scoped work keys, status, parent/dependency references and acceptance expectations. Concurrent creation with the same work key SHALL resolve to one task or an explicit payload conflict. Claiming SHALL atomically assign at most one unexpired owner and return a persisted expiry and fencing token. Tasks with incomplete prerequisites SHALL not be normally claimable. Claims SHALL be renewable and releasable by the current owner only.

#### Scenario: Competing claims
- **WHEN** two agents claim the same available task concurrently
- **THEN** exactly one obtains ownership and the other receives the active ownership/conflict information

### Requirement: Recover abandoned work without stale-owner writes
Claims SHALL expire using server time and persisted expiry, unchanged by restart. A new explicit claim after expiry SHALL issue a new fencing token. Renewal, release, block and completion SHALL require the current unexpired token; late submissions from earlier owners SHALL fail. Documentation SHALL state that server claims cannot prevent external filesystem or tool side effects.

#### Scenario: Agent stops and another takes over
- **WHEN** an owner's lease expires and another agent claims the task
- **THEN** the new owner can inspect prior progress and the old token cannot complete or alter owned task state

### Requirement: Evidence-based completion and handoff
Task completion SHALL atomically record outputs, evidence references, reported verification status and handoff alongside completed state. Missing verification SHALL be labeled unverified, never inferred successful. Blocking SHALL record the blocker and release ownership. Completed tasks SHALL be discoverable and unavailable for claim until explicitly reopened with a reason and audit record.

#### Scenario: Reuse completed work
- **WHEN** another agent inspects a completed task
- **THEN** it can identify the outputs, evidence and verification status without rerunning the task to find what was done

### Requirement: Per-agent checkpoint and bounded catch-up
The server SHALL provide a consistent session snapshot and bounded changes after an agent's acknowledged sequence, including pagination metadata. Read operations SHALL NOT advance checkpoints. Explicit checkpoint writes SHALL acknowledge only a valid consumed sequence for that agent and SHALL survive restart. Invalid or expired cursors SHALL require full resynchronization rather than silently omit changes.

#### Scenario: Other agents contribute during pagination
- **WHEN** an agent pages through changes while another agent commits updates
- **THEN** the current snapshot remains consistent and later changes remain available on subsequent catch-up

### Requirement: Session artifacts are distinct from learning tiers
Session context, plans, task records, findings and checkpoints SHALL remain durable operational artifacts independent of temporary-learning expiry and semantic indexing. Learning promotion SHALL be explicit and retain session provenance. Session membership SHALL NOT expose another repository or session's context.

#### Scenario: Temporary memory expires
- **WHEN** learning cleanup removes an expired temporary record linked by a session
- **THEN** durable task progress and checkpoints remain intact and any unavailable reference is reported honestly
