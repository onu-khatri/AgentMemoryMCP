# Troubleshooting and failure diagnosis

[Previous: Extension guide](18-extension-and-change-guide.md) | [Developer wiki](README.md) | [Next: Build and demo](20-build-run-and-demo.md)

## Server does not start

Check startup validation messages on stderr. Common causes:

- missing or unsafe `Repository__Id`;
- non-absolute, network, device or volume-root `SystemStorage__Root`;
- temp lifetime above 24 hours;
- non-loopback Ollama/Qdrant configuration;
- invalid ports, limits or collection prefix.

Relevant code: `MemoryConfigurationExtensions`, option classes and `MemoryConfigurationTests`.

## MCP client receives malformed JSON-RPC

Ensure the project was built before launch and the client uses `dotnet run --no-build`. Search for writes to stdout. Logging must go to stderr. Run `Test-PublishedMcp.ps1` against a published executable.

## `revision_conflict`

The caller used stale state or the file changed outside the expected transaction. Reread/resume, compare the current revision and preserve concurrent fields. Submit changed intent with a new operation ID.

Inspect `SessionCoordinationService`, `CanonicalMemoryService` and expected hashes in `ManagedTransactionStore`.

## `operation_conflict`

The same operation/event/outcome ID was used for different input. Recover the original exact request or use a new ID for the new logical mutation. Do not delete receipts to force progress.

## `lock_timeout`

Another process holds the repository file lease or a process stalled in managed work. Wait and retry the identical operation ID. Inspect the process list and `.locks` path, but do not delete an actively held lock file as a concurrency workaround.

## `storage_invalid`

Preserve files before intervention. Determine whether canonical data or only a derived catalog is invalid. `memory_rebuild_indexes` repairs derived filesystem indexes; it intentionally leaves corrupt canonical records reported and untouched.

## Session cursor invalid or expired

Start `resume_agent_session` without a cursor. Never skip to a later page or checkpoint the partial old snapshot. Snapshot lifetime defaults to 60 minutes.

## `stale_claim`

Stop task-owned mutations. Resume task state and claim/take over only if the lease/state allows it. Reconcile external side effects because the server cannot fence them.

## Semantic recall degraded

Call `memory_status` and `Test-AgentMemoryDependencies.ps1`. Distinguish:

- Ollama unavailable/timeout/model missing;
- no active compatible collection;
- Qdrant unavailable;
- embedding fingerprint mismatch requiring migration;
- pending embeddings or invalid canonical records.

Canonical and session work can continue. Do not delete canonical files or `.vector` indiscriminately.

## Qdrant was deleted

Start Qdrant, verify dependencies, then run `memory_reindex` with Qdrant/model-migration/all scope as appropriate. Canonical long records rebuild points. Previous collections and alias metadata support controlled rollback.

## Archive or compaction fails

Check source revisions, explicit review state, per-source evidence/contradiction coverage, archive bounds and protected grants. The service preserves originals until compressed data and manifest pass bounded read-back verification.

## `approval_required`

An operator must create the external grant for the exact repository, memory revision and action. MCP tools cannot create it. A new record revision invalidates an older grant by design.

## Background maintenance error

`memory_status` reports last error counts/codes. The worker has a ten-second budget and retries later. Logs exclude content. Run bounded manual cleanup/reindex only after identifying the category.

