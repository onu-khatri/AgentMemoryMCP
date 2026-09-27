## Purpose

Retain recent learning for intentional agent review and auditable compaction, archive, promotion, or deletion.

## ADDED Requirements

### Requirement: Persistent reviewable short-term memory
The server SHALL persist versioned short-term JSON with the metadata defined in source requirement section 7, including review state, review dates, status, counters, confidence, provenance, promotion state and relationships. Records SHALL survive restart without mandatory age-based deletion. Review listing SHALL support age, state, category, decision area, actor, task and bounded result filters.

#### Scenario: Review age is a suggestion
- **WHEN** a record passes review, compaction or archive suggestion age
- **THEN** it is reported as a candidate and remains available until an authorized action changes its state

### Requirement: Record intentional review and safe deletion
Review SHALL support keep, compact_candidate, promotion_candidate, archive_candidate and delete_candidate with actor, rationale and evidence. Explicit deletion SHALL require memory ID, actor and reason and produce an audit event. Protected or audit-required records SHALL reject removal without applicable explicit policy authorization. Marking a delete candidate SHALL NOT itself delete content.

#### Scenario: Keep or mark for later action
- **WHEN** an agent marks a record keep or delete_candidate
- **THEN** its review metadata changes with an audit event and its content remains stored

#### Scenario: Protected deletion
- **WHEN** deletion or compaction source removal targets a protected record without authorization
- **THEN** removal is rejected with a reason and the original is preserved

### Requirement: Agent-supplied compaction with provenance
The server SHALL expose prepare and commit operations and a compact convenience operation. Prepare SHALL return selected sources and revisions. Commit SHALL require an agent-supplied summary, source IDs, facts, open questions, contradictions, successful/failed patterns, evidence, actor and reason. It SHALL validate source coverage and revisions, preserve unresolved contradictions, and archive originals by default only after the compacted record commits. It MUST NOT generate unsupported facts or claim semantic verification solely from structural checks.

#### Scenario: Successful compaction
- **WHEN** an agent commits a reviewed representation of related unchanged sources
- **THEN** one compacted record preserves every source reference and evidence and originals remain recoverable in verified archives

#### Scenario: Source changed after preparation
- **WHEN** any source revision differs at commit
- **THEN** commit returns conflict and leaves all originals unchanged

### Requirement: Verified compressed archives
Archives SHALL contain validated JSON Lines compressed with gzip and an ID/hash manifest. The server SHALL write atomically and fully verify readability and expected record hashes before removing originals. Explicit archived get SHALL locate a record by manifest; ordinary recall SHALL NOT scan archives. Archive reads SHALL enforce decompression limits and path containment.

#### Scenario: Archive corruption or interruption
- **WHEN** archive creation fails verification or the process stops before verified completion
- **THEN** source records remain available and recovery safely resumes or discards incomplete output

#### Scenario: Explicit historical lookup
- **WHEN** get requests an archived ID with archive access enabled
- **THEN** the server returns that archived record with provenance and archive status

### Requirement: Conservative deduplication
Concurrent creation of the same normalized claim in the same repository/tier/context SHALL yield one active record. Merging different IDs SHALL require explicit review of claim identity and preserve evidence and relationships; embedding similarity alone SHALL NOT merge records.

#### Scenario: Related but conflicting observations
- **WHEN** records are similar but contain contradictory claims
- **THEN** they remain distinct or are compacted with the contradiction explicitly retained
