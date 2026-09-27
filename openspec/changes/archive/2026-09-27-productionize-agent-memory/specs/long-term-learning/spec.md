## Purpose

Preserve auditable canonical learning whose validation and reliability depend on explicit evidence and outcomes.

## ADDED Requirements

### Requirement: Canonical candidate lifecycle
Remembering long_candidate or promoting short-term memory SHALL create a canonical candidate file with source provenance, never directly validated learning. The server SHALL represent candidate, validated, superseded, retired and rejected lifecycle independently of pending embedding/index work. All durable records SHALL remain reconstructable without vectors.

#### Scenario: Candidate created during outage
- **WHEN** a candidate is created while embedding or indexing is unavailable
- **THEN** its canonical file persists with candidate lifecycle and pending work state

### Requirement: Evidence-based explicit validation
Candidate validation SHALL be an explicit promotion governed by policy, requiring two independent successful confirmations by default. Independence SHALL require distinct session IDs, agent IDs and evidence references; replays SHALL NOT count again. Configurable single authoritative reproducible verification SHALL be disabled by default. High-risk security, authentication, authorization, secret, destructive-operation, migration, deployment, architecture, public-contract and sensitive-data claims SHALL require explicit policy approval rather than counter-only validation.

#### Scenario: Two independent successes
- **WHEN** one independent success is recorded and validation requested
- **THEN** the record remains candidate with an insufficient-confirmations reason
- **WHEN** a second independent success is recorded and validation requested
- **THEN** an otherwise eligible record becomes validated with evidence and an audit event

#### Scenario: Same source repeated
- **WHEN** successes reuse a session, actor, evidence reference or outcome ID
- **THEN** they do not satisfy the independent-confirmation threshold

#### Scenario: High-risk claim
- **WHEN** a high-risk candidate has two successes but no required approval
- **THEN** validation is rejected and the approval requirement is reported

### Requirement: Replay-safe outcomes and confidence
Outcomes SHALL support success, partial_success, failure, contradicted, not_applicable and stale with outcome ID, actor/session/task, evidence, notes and UTC timestamp. Counters and explicit usage SHALL update once per operation. Confidence SHALL follow the documented deterministic formula and SHALL NOT confer repository authority. Contradicted/stale records SHALL be flagged and excluded from ordinary validated recall pending review.

#### Scenario: Outcome retry
- **WHEN** the same outcome ID is submitted twice
- **THEN** counters and confidence reflect one outcome and the retry returns the existing result

### Requirement: Preserve supersession and retirement history
Supersession and retirement SHALL require reason and evidence, preserve canonical history and replacement relationships, and remove records from ordinary recall immediately even if vector updates are pending. Validated records SHALL be retired rather than physically removed by memory_delete.

#### Scenario: Stale vector after retirement
- **WHEN** Qdrant still returns a point whose canonical record was retired
- **THEN** ordinary recall excludes it and explicit historical get remains available

### Requirement: Learning remains advisory
Retrieved learning SHALL carry lifecycle and provenance and SHALL NOT override current code, tests, requirements, repository instructions, approved plans or explicit user decisions. The server SHALL store concise operational facts/evidence/rationale and SHALL NOT provide private chain-of-thought persistence fields.

#### Scenario: Contrary current evidence
- **WHEN** an agent records a contradicted outcome with current evidence
- **THEN** the memory is marked accordingly without changing repository authority or approvals
