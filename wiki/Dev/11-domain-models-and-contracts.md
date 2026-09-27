# Domain models and contracts

[Previous: Semantic pipeline](10-semantic-pipeline.md) | [Developer wiki](README.md) | [Next: Security](12-security-and-trust-boundaries.md)

## Serialization contract

`MemoryJson.Options` is shared by MCP discovery, request parsing and persistence. It uses camelCase properties, snake_case string enums, disallows numeric enums and unknown members, respects required/nullable declarations, writes indented JSON and limits depth to 32. `SchemaFor<T>` produces JSON Schema 2020-12 and applies data-annotation bounds.

Strict serialization prevents misspelled fields from being silently ignored.

## Session domain

`SharedSessionArtifact` represents `context`, `decision`, `finding`, `handoff`, `plan` or `output`. It carries stable identity, actor, optional task association, title, `data` or typed context, evidence, revision and server timestamp.

`SessionWorkingContext` holds objective, plan references, constraints and next actions. Null means unknown; an empty list means explicitly none.

`CoordinatedTask` holds work key, dependencies, acceptance expectations, next action, status, owner, claim token, fencing generation, expiry, revision, blocker, outputs, evidence, verification status and handoff.

`SessionCoordinationMetadata` is the session root: repository/session identity, monotonic sequence, artifact revisions, tasks, checkpoints and bounded changes.

`SessionSnapshot`, `SnapshotChunk`, `SessionResumePage` and `ResumeEntry` implement immutable paginated resume.

## Memory domain

`MemoryRecord` contains:

- identity, repository and tier;
- session/task/parent/agent provenance;
- category, decision area, title, content, structured data and tags;
- source references and timestamps;
- expiry and review state;
- content hash, revision and operation ID;
- protected flag;
- outcome counters, confirmations and confidence;
- lifecycle relationships and provenance;
- stale/contradicted flags;
- embedding and vector-index state;
- optional compaction details.

`MemoryRecordValidator.Validate` enforces schema version, identifiers, UTC timestamps, tier/lifecycle combinations, temp expiry, reliability metadata, confirmation independence, embedding metadata and compaction invariants.

## Important enums

| Enum | Values and purpose |
| --- | --- |
| `MemoryTier` | temp, short, long canonical tier |
| `RememberTier` | temp, short, long_candidate creation intent |
| `ReviewState` | unreviewed plus explicit review decisions |
| `MemoryState` | active, compacted, archived, deleted, promoted, candidate, validated, superseded, retired, rejected |
| `OutcomeKind` | success, partial success, failure, contradicted, not applicable, stale |
| `EmbeddingState` | not required, pending, ready, failed |
| `VectorIndexState` | not required, pending, indexed, failed |
| `RelationshipKind` | duplicate, related, derived, supersession, compaction and promotion links |
| `TaskAction` | create, claim, renew, release, block, complete, reopen |
| `WorkStatus` | available, in progress, blocked, completed |
| `MemoryReindexScope` | filesystem, Qdrant, pending, selected IDs, migration, all |

## Request contracts

Contracts under [Contracts/Requests](../../AgentSession.MCP/Contracts/Requests/) define typed tool inputs. Records are used where constructor-required shape is helpful; mutable classes are used where defaults and optional collections improve generated schemas.

Important concurrency fields:

- `operationId` gives replay identity to logical mutations;
- `expectedRevision` prevents stale replacement;
- `claimToken` fences task-owner operations;
- `outcomeId` and `eventId` are replay identities for those records;
- `preparationToken` binds compaction commit to exact sources.

## Response contracts

Responses return structured objects, never YAML strings or untyped prose. Examples include `SessionMutationResult`, `SessionResumePage`, `MemoryWriteResult`, `MemoryReadResult`, `RecallMemoryResult`, `ArchiveMemoryResult`, `CompactionResult`, `ReindexMemoryResult` and `MemoryStatusResult`.

## Schema artifacts

The JSON schemas under [AiLearning/schemas](../../AiLearning/schemas/) document persisted and MCP shapes. When changing a public or persisted model, update its schema, compatibility reasoning, round-trip tests and MCP discovery assertions together.

