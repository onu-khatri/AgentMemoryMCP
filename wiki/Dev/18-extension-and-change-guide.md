# Extension and change guide

[Previous: Technology decisions](17-technology-and-design-decisions.md) | [Developer wiki](README.md) | [Next: Troubleshooting](19-troubleshooting.md)

## Add a new MCP tool

1. Confirm the behavior cannot be expressed by an existing tool.
2. Add a bounded typed request/response contract.
3. Put behavior in a service, not the tool class.
4. Validate IDs, enums, sizes, nesting and sensitive content.
5. Define revision and idempotency behavior for mutations.
6. Use `ManagedTransactionStore` for managed writes.
7. Add an accurate description with “Use” and “Do not use” boundaries.
8. Set MCP annotations truthfully.
9. Register through an existing tool class or one intentional new registration path.
10. Add protocol discovery/schema/call tests for supported versions.
11. Update `AiLearning/MCP-CONTRACT.md`, agent guidance and this wiki's flow page.

## Add a persisted field

Decide whether the field is canonical, derived or response-only. For canonical fields:

- define default/backward-reading behavior or bump schema version;
- add validator rules;
- include it in hashes if semantic identity depends on it;
- update schemas and round-trip tests;
- consider archive, compaction, migration and rebuild paths;
- ensure content policy inspects it.

## Add a new artifact kind

Update `SessionArtifactKind`, validation in `SessionCoordinationService`, snapshot ordering/unknown handling in `SessionResumeService`, JSON schemas, examples and MCP protocol tests. Decide whether kind/task association remains immutable and whether a task claim is required.

## Add a new lifecycle state

Update `MemoryState`, `MemoryRecordValidator`, `LearningCatalog.FileFor`, ordinary recall eligibility, history flags, vector payload/filter rules, status counts, migration/reconciliation and every exhaustive switch. Add tests proving stale vector points cannot leak the new excluded state.

## Change embedding projection or model

Never reuse the old fingerprint. Change `EmbeddingProjection.Version` when projection text semantics change. Use `VectorMigrationService` to build a new physical collection and switch the alias only after completeness verification.

## Add another embedding provider

Introduce a provider abstraction, provider-specific identity and strict adapter validation. Keep fingerprint fields provider/model/digest/dimension/projection. Preserve loopback/local policy unless requirements explicitly change. Test timeout, cancellation, batch cardinality, dimensions, finite floats and oversized input.

## Replace filesystem canonical storage

A backend must preserve:

- repository/session isolation;
- atomic multi-record visibility;
- optimistic revisions;
- operation receipts and conflict detection;
- crash recovery after durable intent;
- bounded scans and rebuild;
- archive integrity and external grants;
- testable time and fault boundaries.

Do not treat an ORM transaction alone as equivalent until retry and cross-process semantics are demonstrated.

## Change task coordination

Maintain stable work-key deduplication, dependencies, server-time leases, claim-token fencing, evidence-based completion and audited reopen. Any distributed transport will also need authenticated actor identity; current actor IDs are provenance only.

## Documentation rule

Update file/class/method references without line numbers. Add or change Mermaid flows when control or persistence paths change. Keep developer docs, consumer docs, schemas and acceptance evidence synchronized.

