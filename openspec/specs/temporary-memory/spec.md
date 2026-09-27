## Purpose

Provide restart-safe temporary working records with a strictly bounded readable lifetime and deterministic cleanup.

## Requirements
### Requirement: Persist and manage temporary records
The server SHALL persist temporary records as inspectable versioned JSON with repository/session/task/parent-step/agent provenance, title, content, structured data, tags, category, source reference/type, timestamps, content hash and revision. It SHALL support creation, get, update, list/filter, recent recall, explicit deletion and promotion to short-term without embeddings.

#### Scenario: Restart retains working context
- **WHEN** an unexpired record is created and the process restarts
- **THEN** the same record and provenance remain retrievable from its file without a vector service

#### Scenario: Update conflicts
- **WHEN** an update supplies an older revision than the stored record
- **THEN** the server returns a conflict without overwriting content or timestamps

### Requirement: Enforce persisted expiry
The server SHALL enforce expiry no later than createdAtUtc plus 24 hours and any earlier configured or explicit expiry. Updates and restarts MUST NOT extend expiry. All read paths SHALL exclude expired records even before physical cleanup. Invalid lifetime options or attempted expiry extensions SHALL be rejected.

#### Scenario: Boundary and delayed cleanup
- **WHEN** time reaches the record's effective expiry while cleanup has not yet run
- **THEN** recall, list, and get do not return the record content

#### Scenario: Restart after expiry
- **WHEN** the server restarts after a record expired
- **THEN** it hides the record immediately and removes the expired file during startup cleanup

### Requirement: Idempotent expiration cleanup
The server SHALL perform scheduled and opportunistic expiration cleanup, repair derived indexes, and report deleted/error counts. No background process SHALL depend on embedding services to clean temporary files. Physical cleanup while the host is stopped is not guaranteed and SHALL be documented.

#### Scenario: Repeated sweep
- **WHEN** cleanup runs twice against the same expired records
- **THEN** the first run removes the files and the second reports no additional deletions without errors

### Requirement: Promotion preserves provenance
Temporary promotion SHALL create short-term memory with source relationships and preserve evidence before marking the source promoted or removing it. Repeating the operation SHALL return the same destination.

#### Scenario: Retry after interrupted promotion
- **WHEN** promotion is retried after the destination committed but before the response arrived
- **THEN** one short-term record exists and its source link remains intact
