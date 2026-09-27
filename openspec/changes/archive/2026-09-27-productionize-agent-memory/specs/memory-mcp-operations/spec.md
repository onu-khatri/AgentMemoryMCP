## Purpose

Expose a reliable local MCP memory service with validated configuration, useful health, and demonstrated operational readiness.

## ADDED Requirements

### Requirement: Typed MCP tools and clean transport
The server SHALL expose memory_status, memory_remember, memory_recall, memory_get, memory_list_review, memory_mark_reviewed, memory_compact, memory_delete, memory_archive, memory_record_event, memory_record_outcome, memory_promote, memory_supersede, memory_retire, memory_reindex and memory_cleanup. It SHALL additionally expose memory_rebuild_indexes, memory_prepare_compaction, memory_commit_compaction and memory_update. Schemas SHALL describe typed inputs/results, bounded values, errors, provenance, revisions and idempotency. Stdio stdout SHALL contain only protocol messages; diagnostics SHALL go to stderr. Calls SHALL propagate cancellation and discover each tool once.

#### Scenario: Real client lifecycle
- **WHEN** a supported MCP client initializes, enumerates tools and performs a valid memory call
- **THEN** schemas and results are usable and stdout contains no application/build diagnostics

#### Scenario: Malformed or cancelled call
- **WHEN** a request is malformed or cancelled before commit
- **THEN** the server returns an appropriate protocol/tool error or cancellation without partial visible mutation or secret disclosure

### Requirement: Validated local configuration
Configuration SHALL support central root, stable repository identity, retention/review policies, limits, confirmation count, similarity/result bounds, Ollama model/timeout/base URL and Qdrant endpoint/collection through files and environment overrides. Defaults SHALL be local with no cloud credentials, stdio transport, hard temp maximum 24h and 60-minute sweep, 3/7/14-day short suggestions, two confirmations and ten semantic results. Invalid identity, escaped roots, non-loopback dependencies or out-of-range values SHALL fail clearly.

#### Scenario: Invalid temporary lifetime
- **WHEN** configuration requests more than 24 hours
- **THEN** startup validation rejects it rather than weakening the retention guarantee

### Requirement: Health and deterministic maintenance
Status SHALL report server version, configured central/repository roots, tier/state/expired/review/pending counts, dependency and model/collection/index health, last cleanup/compaction/archive and recovery backlog. Maintenance SHALL clean expired temp, repair indexes, rotate journals, verify archives, process explicitly approved archive candidates and reconcile pending/orphan vectors. Background work SHALL NOT invent summaries, validate candidates, resolve contradictions or delete uncertain knowledge.

#### Scenario: Reviewable knowledge is old
- **WHEN** scheduled maintenance finds an old unreviewed short-term record
- **THEN** it reports a suggestion without semantic deletion or promotion

### Requirement: Reproducible local operations
The project SHALL provide localhost-only Qdrant Compose with persistent central vector storage and PowerShell start/stop/check with safe transition documentation. Documentation SHALL cover model pull, client launch, identity selection, folder inspection, review/cleanup/compaction/archive/rebuild, backup/restore and safe isolated index reset. Scripts SHALL NOT install system-wide dependencies automatically.

#### Scenario: Missing dependency
- **WHEN** a check finds Docker, Ollama or embeddinggemma unavailable
- **THEN** it reports the missing prerequisite and actionable commands without claiming successful semantic readiness

### Requirement: Evidence-based release verification
Production completion SHALL require all AC1-AC20 and the 28-step source verification sequence with exact commands and results. Automated coverage SHALL include real filesystem/restart, concurrency, crash recovery, security, compatibility and protocol tests plus real Ollama/Qdrant paraphrase/rebuild/outage tests. Reports SHALL distinguish implemented, unit-tested, integration-tested, end-to-end verified and unverified; infrastructure skips SHALL NOT count as passes.

#### Scenario: Only baseline tests pass
- **WHEN** existing tests pass but real embedding/vector tests have not run
- **THEN** the report states those integrations remain unverified and does not declare production completion

### Requirement: Session resumption and coordination tools
The server SHALL expose resume_agent_session, checkpoint_agent_session and coordinate_agent_task, and structured update contracts for append_agent_memory. Contracts SHALL describe snapshot pagination, per-agent checkpoints, artifact revisions, task work keys, lease tokens and claim conflicts. Session resume, updates and task coordination SHALL operate without Ollama or Qdrant. Documentation SHALL lead with join/resume, inspect prior work, claim, contribute, complete/handoff and checkpoint workflows.

#### Scenario: Shared session without semantic services
- **WHEN** two agents join the same session while Ollama and Qdrant are offline
- **THEN** they can resume, inspect prior outputs, claim distinct tasks, publish contributions and checkpoint with explicit semantic-service degradation only

### Requirement: Primary outcome verification
Release verification SHALL include SC1-SC6 from design.md in addition to source AC1-AC20: restart-safe resume, shared contribution visibility, atomic claims and stale-owner rejection, checkpoint catch-up, semantic-service independence and requirement-driven contract transitions with historical-data preservation.

#### Scenario: Learning tests pass but coordination fails
- **WHEN** vector recall passes but concurrent claims or restart resumption fail
- **THEN** production readiness is not declared
