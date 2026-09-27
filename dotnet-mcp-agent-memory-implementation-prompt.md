# Implementation Prompt — Local .NET MCP Agent Memory & Learning Server

## Mission

Build a complete, production-quality, repository-local **Agent Memory & Learning MCP Server in .NET**.

The server will be used by parent agents and delegated sub-agents working in this repository. It must provide three memory tiers:

1. **Temporary memory** — filesystem files that live for at most 24 hours.
2. **Short-term memory** — filesystem files that persist until reviewed, deleted, compacted, archived, or promoted by agents according to policy.
3. **Long-term memory** — durable canonical learning records plus local semantic search using embeddings stored in a local vector database.

The complete system must run locally on a developer laptop without requiring a cloud AI or cloud database.

The MCP server must be the controlled programmatic write boundary for the managed `AiLearning/` memory hierarchy.

---

# 1. Repository-first operating rules

Before editing or creating code:

1. Read the repository root `AGENTS.md`.
2. Read:
   - `AiLearning/README.md`
   - `AiLearning/INDEX.md`
   - `AiLearning/MCP-CONTRACT.md`
   - `AiLearning/schemas/*`
   - any repository-local implementation plans or relevant instructions.
3. Inspect the existing solution structure and reuse existing:
   - target framework;
   - dependency injection;
   - configuration;
   - logging;
   - testing;
   - architecture;
   - naming;
   - error handling;
   - serialization patterns.
4. Prefer repository evidence over assumptions.
5. Do not create competing architecture when an existing extension point is available.
6. Preserve all approval, safety, ownership, and implementation-plan gates already defined by repository instructions.

Work autonomously.

Do not ask the user routine implementation questions. Ask only when a material unresolved decision, authorization requirement, destructive action, or repository-policy conflict makes safe progress impossible.

---

# 2. Core architecture

Implement this lifecycle:

```text
Agent / Sub-agent
      |
      v
Temporary Memory (files, <= 24h)
      |
      | useful beyond current working window
      v
Short-Term Memory (files)
      |
      | review / dedupe / compact / validate
      +-----------------------+
      |                       |
      | discard/archive       | promote
      v                       v
deleted/archive       Long-Term Candidate
                              |
                              | validated
                              v
                       Long-Term Memory
                 canonical files + embeddings
                              |
                              v
                     Qdrant vector index
                              |
                              v
                   semantic future recall
                              |
                              v
                      outcome feedback
                              |
             reinforce / supersede / retire
```

Memory is **advisory learning**, not repository authority.

Memory must never override:

- `AGENTS.md`;
- approved requirements;
- approved implementation plans;
- ADRs;
- repository policy;
- current code;
- current tests;
- current runtime evidence;
- explicit user decisions.

When memory conflicts with stronger current evidence, the agent must prefer current authority/evidence and mark the learned memory as stale, contradicted, superseded, or retired as appropriate.

---

# 3. Required technology stack

Use the repository's existing .NET target framework when possible.

If this is a standalone implementation with no framework constraint, use the current .NET LTS release.

Use:

- official `ModelContextProtocol` C# SDK;
- `Microsoft.Extensions.Hosting`;
- Microsoft dependency injection;
- `Microsoft.Extensions.Options`;
- `Microsoft.Extensions.Logging`;
- `System.Text.Json`;
- filesystem storage for temporary memory;
- filesystem storage for short-term memory;
- filesystem storage for canonical long-term records;
- **Qdrant** for long-term vector indexing;
- official `Qdrant.Client` package;
- **Ollama** for local embeddings;
- `HttpClientFactory` for Ollama;
- repository test framework if present, otherwise xUnit.

Do **not** use SQLite as the primary memory store.

Operational metadata may use small files/index manifests where needed, but temporary and short-term memory must remain filesystem-native.

---

# 4. Local infrastructure

## MCP transport

Primary transport:

```text
stdio
```

Requirements:

- MCP protocol output only on stdout.
- Application/logging output must go to stderr.
- Optionally support localhost HTTP if it fits cleanly.
- Do not expose HTTP externally by default.

## Embeddings

Default local embedding provider:

```text
Ollama
```

Default base URL:

```text
http://localhost:11434
```

Default model:

```text
embeddinggemma
```

Create an abstraction similar to:

```csharp
public interface IEmbeddingProvider
{
    Task<EmbeddingResult> EmbedAsync(
        string text,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EmbeddingResult>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken);

    Task<EmbeddingProviderHealth> CheckHealthAsync(
        CancellationToken cancellationToken);
}
```

Implement:

```text
OllamaEmbeddingProvider
```

Do not hard-code embedding dimensions.

Detect the actual vector dimension from the configured model and validate Qdrant collection compatibility.

Never silently mix incompatible embedding models or vector dimensions.

## Vector database

Use:

```text
Qdrant
```

Default:

```text
REST  localhost:6333
gRPC  localhost:6334
```

Prefer the official gRPC .NET client.

Qdrant is a **rebuildable semantic index**, not the canonical store.

Long-term canonical files must remain sufficient to reconstruct Qdrant from scratch.

---

# 5. Required `AiLearning/` layout

Use or adapt the existing repository structure.

Conceptual target:

```text
AiLearning/
├── README.md
├── INDEX.md
├── MCP-CONTRACT.md
│
├── temp/
│   ├── sessions/
│   │   └── <session-id>/
│   │       └── <memory-id>.json
│   └── .index.json
│
├── short-term/
│   ├── active/
│   │   ├── <yyyy-mm>/
│   │   │   └── <memory-id>.json
│   ├── compacted/
│   │   └── <yyyy-mm>/
│   │       └── <compact-memory-id>.json
│   ├── archive/
│   │   └── <yyyy-mm>/
│   │       └── <archive-file>.jsonl.gz
│   └── .index.json
│
├── long-term/
│   ├── candidates/
│   │   └── <memory-id>.json
│   ├── records/
│   │   └── <memory-id>.json
│   ├── superseded/
│   └── retired/
│
├── events/
│   └── <yyyy-mm>/
│       └── events.jsonl
│
├── summaries/
│
├── schemas/
│
└── .vector/
    └── qdrant/
```

Runtime-generated indexes must be rebuildable from memory files where practical.

Use `.gitignore` deliberately for machine-local runtime directories.

Do not automatically ignore durable canonical long-term learning unless repository policy explicitly says long-term learning is machine-local only.

---

# 6. Memory Tier 1 — Temporary filesystem memory

Temporary memory is **not in-process-only memory**.

It must be stored as files so parent agents and sub-agents can coordinate through the MCP server and the data can survive process restarts during the same working day.

## Lifetime

Every temporary memory file must live for **no more than 24 hours**.

Default:

```text
TempMemoryMaxAgeHours = 24
```

This is a hard upper bound.

An entry may have an earlier explicit expiration, but never a later one.

The server must automatically clean temporary memory whose age/expiration exceeds 24 hours.

A server restart must **not** reset the expiration clock.

Expiration must be based on persisted timestamps in the file.

## Temp record

Each temp record should contain at least:

```text
id
schemaVersion
repositoryId
sessionId
taskId
parentStepId
agentId
category
title
content
structuredData
tags
sourceType
sourceReference
createdAtUtc
updatedAtUtc
lastAccessedAtUtc
expiresAtUtc
contentHash
```

`expiresAtUtc` must satisfy:

```text
expiresAtUtc <= createdAtUtc + 24 hours
```

## Temp behavior

Support:

- remember;
- get;
- list;
- exact/metadata search;
- recent recall;
- update;
- promote to short-term;
- explicit delete;
- automatic expiration cleanup.

Do not generate embeddings for temporary memory by default.

Do not place temporary memory in Qdrant.

## Temp cleanup

Implement a hosted cleanup service.

Recommended default scan interval:

```text
60 minutes
```

Also perform opportunistic cleanup before/after relevant MCP operations.

Cleanup must:

1. detect expired files;
2. delete them safely;
3. repair/rebuild temp index metadata when needed;
4. report counts;
5. remain idempotent.

A temp entry older than 24 hours must never be returned by normal recall, even if filesystem cleanup has not yet physically deleted it.

---

# 7. Memory Tier 2 — Short-term filesystem memory

Short-term memory is also stored in the filesystem.

It is persistent across MCP server restart.

Unlike temp memory, short-term memory does **not** have a mandatory fixed 24-hour deletion period.

It exists so agents can retain recent working knowledge and periodically curate it.

Examples:

- recent task outcomes;
- unresolved observations;
- recent repository discoveries;
- repeated errors and fixes;
- user decisions that are safe and appropriate to persist;
- reusable tactics awaiting validation;
- recent coordination context;
- learning candidates;
- knowledge worth keeping for several days/weeks but not yet proven durable.

## Short-term file record

Each record should contain at least:

```text
id
schemaVersion
repositoryId
sessionId
taskId
parentStepId
agentId
category
decisionArea
title
content
structuredData
tags
sourceType
sourceReferences
createdAtUtc
updatedAtUtc
lastAccessedAtUtc
reviewAfterUtc
lastReviewedAtUtc
reviewState
contentHash
status
successCount
failureCount
useCount
confidence
promotionState
relatedMemoryIds
```

Possible `reviewState` values:

```text
unreviewed
keep
compact_candidate
promotion_candidate
archive_candidate
delete_candidate
reviewed
```

Possible `status` values:

```text
active
compacted
archived
deleted
promoted
superseded
```

## Agent review

Agents must be able to intentionally review short-term memory.

Implement tools/services that support:

- list memories needing review;
- inspect an individual memory;
- mark keep;
- mark for deletion;
- delete;
- archive;
- compact;
- merge duplicates;
- promote to long-term candidate;
- record rationale/evidence for the action.

Agents must not be forced to ask the user before routine safe cleanup of clearly stale, duplicate, expired-by-policy, or successfully compacted short-term memory.

Follow repository/user approval rules if deletion would remove information explicitly designated as protected or required for audit.

---

# 8. Short-term compaction

Short-term memory must support **periodic compaction**.

Compaction has two distinct meanings and both should be supported.

## A. Semantic compaction

Semantic compaction reduces many related memory files into a smaller, useful representation.

Example:

```text
12 repetitive short-term observations
        |
        v
1 compacted summary record
+ references to original memory IDs
```

Semantic compaction must:

1. group only clearly related memories;
2. preserve important facts and unresolved contradictions;
3. preserve references to all source memory IDs;
4. record the compaction timestamp;
5. record the agent/tool responsible;
6. avoid inventing new facts;
7. avoid converting uncertain observations into facts;
8. preserve evidence references;
9. be reversible/auditable through source IDs or archive.

The compacted record should contain:

```text
id
sourceMemoryIds
title
summary
facts
openQuestions
contradictions
successfulPatterns
failedPatterns
tags
decisionArea
createdAtUtc
compactedAtUtc
contentHash
```

After successful compaction, originals may be:

```text
archive
or
delete
```

based on policy.

Default safe behavior:

```text
compact -> archive originals
```

Do not immediately destroy originals unless policy explicitly allows it.

## B. Physical file compression

Archived short-term records may be physically compressed.

Preferred format:

```text
JSON Lines + gzip
```

Example:

```text
AiLearning/short-term/archive/2026-09/short-memory-2026-09-20.jsonl.gz
```

Requirements:

- deterministic serialization where practical;
- validate all records before archival;
- write archive atomically;
- verify archive readability before deleting source files;
- maintain a manifest of archived memory IDs;
- support retrieving an archived record by ID when explicitly requested;
- ordinary recall should prefer active/compacted records and not scan all archives.

Do not gzip active individual memory files if doing so makes routine review/edit/delete operations difficult.

---

# 9. Scheduled short-term maintenance

Implement a maintenance policy that can run periodically.

The server should identify:

```text
unreviewed memories
old active memories
duplicates
highly related clusters
promotion candidates
archive candidates
delete candidates
```

Do not let an autonomous background process perform semantic deletion/promotions requiring judgment unless deterministic policy permits it.

Recommended pattern:

```text
background service
    |
    +--> identifies review candidates
    +--> performs deterministic safe cleanup
    +--> creates maintenance suggestions
    |
agent
    |
    +--> reviews suggestions
    +--> compact/delete/archive/promote
```

Safe automatic tasks may include:

- rebuilding indexes;
- removing broken temp index entries;
- deleting temp memories past 24 hours;
- rotating event journals;
- physically compressing files already marked `archive_candidate`;
- checking archive integrity.

Agent-driven tasks should include:

- semantic compaction;
- uncertain deletion;
- long-term promotion;
- contradiction resolution.

---

# 10. Memory Tier 3 — Long-term memory

Long-term memory contains reusable, validated learning.

Long-term memory must use:

```text
canonical files
+
local embeddings
+
Qdrant semantic index
```

## Canonical source of truth

Canonical long-term records live under:

```text
AiLearning/long-term/
```

Qdrant must never be the only copy.

Long-term records must be human-auditable and source-control compatible when repository policy allows.

## Candidate and validated states

Use lifecycle states:

```text
candidate
validated
superseded
retired
rejected
pending_embedding
```

A short-term record can be promoted to:

```text
long-term candidate
```

but not automatically to validated long-term learning unless validation rules are met.

Default validation rule:

```text
2 independent successful confirmations
```

Make this configurable.

A deterministic repository fact may be validated with one authoritative reproducible verification when policy allows.

Do not auto-promote high-risk claims involving:

- security;
- authentication;
- authorization;
- secrets;
- destructive operations;
- migrations;
- deployments;
- shared architecture boundaries;
- public contracts;
- sensitive user data.

---

# 11. Long-term embeddings

Use Ollama locally.

Default model:

```text
embeddinggemma
```

Store embedding metadata:

```text
provider
model
dimension
embeddingVersion
indexedAtUtc
```

Embedding rules:

- do not embed temp memory;
- do not embed all short-term memory automatically;
- embed long-term candidates/validated records when needed;
- never embed secrets;
- use the same model for indexing and querying a collection;
- model changes require controlled reindexing/versioned collection.

---

# 12. Qdrant design

Default collection:

```text
repo_ai_learning_v1
```

Use cosine similarity unless the embedding model requires otherwise.

Qdrant payload should include:

```text
memoryId
repositoryId
category
decisionArea
status
tags
confidence
successCount
failureCount
useCount
createdAtUtc
updatedAtUtc
validatedAtUtc
sourceType
embeddingProvider
embeddingModel
embeddingVersion
contentHash
```

Search must support metadata filtering.

Qdrant must be reconstructable entirely from canonical long-term files.

Provide `memory_reindex` that can rebuild the collection.

---

# 13. Unified recall behavior

Implement:

```csharp
Task<MemoryRecallResult> RecallAsync(
    MemoryRecallQuery query,
    CancellationToken cancellationToken);
```

Query should support:

```text
query
tiers
repositoryId
sessionId
taskId
decisionAreas
categories
tags
status
maxResults
minimumSimilarity
includeArchived
includeRetired
```

Default recall order:

1. non-expired temp filesystem memory;
2. active/compacted short-term filesystem memory;
3. validated long-term Qdrant semantic memory.

For short-term memory, use a lightweight local lexical/index approach appropriate for files.

Possible approaches:

- generated manifest/index file;
- inverted token index;
- bounded scan for small repositories.

Prefer simplicity and correctness.

Do not introduce a separate database merely for short-term search unless repository evidence proves it is necessary.

Ordinary recall must not scan all compressed archives.

Archived memories should only be opened when explicitly requested or when index metadata identifies a specific archived record.

---

# 14. File indexes

Filesystem storage still needs efficient lookup.

Create rebuildable indexes such as:

```text
AiLearning/temp/.index.json
AiLearning/short-term/.index.json
```

Index entries may contain:

```text
memoryId
relativePath
status
repositoryId
sessionId
taskId
category
decisionArea
tags
createdAtUtc
updatedAtUtc
expiresAtUtc
reviewAfterUtc
contentHash
archiveLocation
```

Requirements:

- index is not canonical;
- memory file is canonical;
- index can be rebuilt by scanning the applicable folder;
- update index atomically;
- handle crash recovery;
- detect missing/orphaned entries;
- support explicit `memory_rebuild_indexes`.

Use locking/concurrency protection.

---

# 15. Deduplication

Calculate a normalized content hash.

Before persistent creation:

1. normalize relevant content;
2. calculate hash;
3. check exact duplicate;
4. optionally compare semantic similarity for long-term candidates;
5. merge evidence/outcomes only when the underlying claim is truly the same.

Support links:

```text
duplicate_of
related_to
derived_from
supersedes
superseded_by
compacted_from
promoted_from
```

Do not merge different claims just because embeddings are similar.

---

# 16. Feedback loop

Whenever retrieved memory materially influences an agent action, allow an outcome to be recorded.

Outcome types:

```text
success
partial_success
failure
contradicted
not_applicable
stale
```

Outcome:

```text
outcomeId
memoryId
sessionId
taskId
agentId
result
evidence
notes
createdAtUtc
```

Track:

```text
successCount
failureCount
useCount
lastUsedAtUtc
lastSuccessfulAtUtc
lastFailedAtUtc
independentConfirmations
confidence
```

Confidence is a retrieval/reliability signal only.

It must never become an authority score.

Repeated confirmations from the same session/agent must not automatically count as independent confirmations.

Use a deterministic documented confidence formula.

---

# 17. MCP tools

Expose strongly typed MCP tools.

At minimum implement the following.

## `memory_status`

Returns:

- server version;
- configured `AiLearning` root;
- temp record count;
- temp expired count;
- short-term active count;
- short-term review-due count;
- short-term compacted count;
- short-term archived count;
- long-term candidate count;
- long-term validated count;
- pending embedding count;
- Ollama health;
- Qdrant health;
- active embedding model;
- active vector collection;
- index health;
- last cleanup;
- last compaction/archive operation.

---

## `memory_remember`

Inputs:

```text
tier: temp | short | long_candidate
content
title?
repositoryId?
sessionId?
taskId?
parentStepId?
agentId?
category?
decisionArea?
tags?
sourceType?
sourceReference?
structuredData?
reviewAfterUtc?
expiresAtUtc?
```

Rules:

- `temp` must be capped at 24 hours.
- `short` creates a filesystem record.
- `long_candidate` creates a candidate, never directly validated memory.

---

## `memory_recall`

Unified recall across requested tiers.

---

## `memory_get`

Retrieve one record by ID.

Must locate:

- temp;
- active short-term;
- compacted short-term;
- archived short-term when explicitly allowed;
- long-term candidate;
- validated;
- superseded;
- retired.

---

## `memory_list_review`

Return short-term memories needing agent review.

Filters:

```text
olderThan
reviewState
category
decisionArea
agentId
taskId
maxResults
```

---

## `memory_mark_reviewed`

Record review action and rationale.

Actions:

```text
keep
compact_candidate
promotion_candidate
archive_candidate
delete_candidate
```

---

## `memory_compact`

Semantically compact selected short-term memory IDs.

Inputs:

```text
memoryIds
agentId
sessionId
taskId
compactionReason
archiveOriginals: true by default
```

Output:

- compacted memory ID;
- sources;
- archive/delete actions;
- any contradictions detected.

The implementation must not fabricate facts during compaction.

If an LLM is required for semantic summarization, the MCP server should expose deterministic storage operations while the calling agent supplies the compacted summary. Do not force the server itself to depend on a generative model just to compact files.

Recommended design:

1. `memory_prepare_compaction` returns source records.
2. agent creates a proposed compacted representation.
3. `memory_commit_compaction` validates and stores it.
4. originals are archived only after successful commit.

---

## `memory_delete`

Allow safe deletion of:

- temp memory;
- short-term memory.

Require:

```text
memoryId
reason
agentId
```

For long-term validated memory, prefer retirement rather than physical deletion.

Deletion must write an audit event.

---

## `memory_archive`

Archive selected short-term records.

Default archive format:

```text
JSONL + gzip
```

Verify archive before removing originals.

---

## `memory_record_event`

Write a memory lifecycle/audit event.

---

## `memory_record_outcome`

Record the actual result from using a memory.

---

## `memory_promote`

Supported transitions:

```text
temp -> short
short -> long_candidate
long_candidate -> validated
```

Server validates policy.

Rejected promotions must return the reason.

---

## `memory_supersede`

Supersede prior long-term learning with a corrected/newer record.

Preserve history.

---

## `memory_retire`

Retire long-term learning.

Inputs:

```text
reason
evidence
replacementMemoryId?
```

Retired memories disappear from ordinary recall.

---

## `memory_reindex`

Support:

```text
rebuild temp index
rebuild short-term index
rebuild Qdrant
pending embeddings
specific memory IDs
model migration
all
```

Must be idempotent.

---

## `memory_cleanup`

Perform deterministic maintenance:

- delete expired temp files;
- repair temp index;
- repair short-term index;
- process already-approved archive candidates;
- rotate event files;
- find orphaned vector entries;
- detect missing long-term vectors;
- verify canonical/index consistency.

Do not silently make semantic deletion decisions.

---

# 18. File safety

Create an abstraction such as:

```csharp
public interface IAiLearningFileStore
{
    // controlled AiLearning filesystem operations
}
```

Requirements:

- one configured repository root;
- canonicalize paths;
- reject traversal;
- never write outside `AiLearning/`;
- atomic writes;
- temporary file + atomic rename/replace;
- UTF-8;
- cancellation support;
- concurrency protection;
- validate records before commit;
- verify archive integrity;
- no generic arbitrary path write MCP tool.

---

# 19. Concurrency

Multiple parent/sub-agents may call the server simultaneously.

Protect:

- duplicate creation;
- index updates;
- file mutation;
- compaction;
- deletion;
- archival;
- promotion;
- outcome counters;
- supersession;
- retirement;
- embedding/indexing.

Use:

- keyed async locks;
- version/ETag fields where useful;
- atomic filesystem operations;
- idempotency keys/event IDs.

Two agents creating the same short-term memory concurrently must not create uncontrolled duplicates.

---

# 20. Event journal

Keep append-oriented events under:

```text
AiLearning/events/<yyyy-mm>/events.jsonl
```

Events:

```text
memory-created
memory-updated
memory-reviewed
memory-deleted
memory-archived
memory-compacted
memory-promoted
memory-used
outcome-recorded
memory-validated
memory-superseded
memory-retired
embedding-pending
embedding-created
vector-indexed
vector-reindexed
cleanup
index-rebuilt
```

Each event:

```text
eventId
memoryId
timestampUtc
agentId
sessionId
taskId
eventType
metadata
```

Do not store private chain-of-thought.

---

# 21. No chain-of-thought persistence

Never persist hidden reasoning or scratchpad chain-of-thought.

Persist only useful operational artifacts:

- factual observations;
- tool results;
- errors;
- resolutions;
- decisions;
- concise rationale;
- evidence;
- reusable tactics;
- outcomes;
- source references;
- status.

---

# 22. Sensitive-data policy

Implement:

```csharp
IMemoryContentPolicy
```

Before filesystem persistence or embedding:

detect/reject/redact common:

- passwords;
- API keys;
- tokens;
- authorization headers;
- private keys;
- secret-bearing connection strings.

Secrets must not enter:

- temp files;
- short-term files;
- archives;
- long-term files;
- event files;
- Qdrant payloads;
- embedding requests.

Embedding secret material is prohibited.

---

# 23. Degraded operation

## Ollama unavailable

Allow:

- temp memory;
- short-term memory;
- filesystem lookup;
- lexical recall;
- canonical long-term candidate creation.

Mark long-term records needing embeddings:

```text
pending_embedding
```

Do not create fake vectors.

## Qdrant unavailable

Preserve long-term canonical records.

Mark indexing pending.

Recall from temp/short still works.

Return degraded health.

## Archive corruption

Never delete source short-term files until archive creation and verification succeeds.

## Index corruption

Filesystem records remain canonical.

Rebuild indexes.

---

# 24. Embedding model migration

If model configuration changes:

- detect model identity/dimension mismatch;
- do not mix incompatible vectors;
- create a versioned Qdrant collection or perform controlled reindex;
- rebuild from canonical long-term records;
- switch active collection only after successful rebuild;
- document recovery/rollback.

---

# 25. Configuration

Use strongly typed options.

Support values equivalent to:

```text
AiLearning:Root

Memory:Temp:MaxAgeHours = 24
Memory:Temp:CleanupIntervalMinutes = 60
Memory:Temp:MaxEntriesPerSession

Memory:Short:ReviewAfterDays
Memory:Short:ArchiveAfterDays
Memory:Short:CompactionCandidateAfterDays
Memory:Short:MaxActiveRecords
Memory:Short:ArchiveCompression = gzip

Memory:Long:RequiredIndependentConfirmations = 2
Memory:Long:MinimumSimilarity
Memory:Long:DefaultMaxResults

Embedding:Provider = ollama
Embedding:Ollama:BaseUrl = http://localhost:11434
Embedding:Ollama:Model = embeddinggemma
Embedding:TimeoutSeconds

Qdrant:Host = localhost
Qdrant:GrpcPort = 6334
Qdrant:RestPort = 6333
Qdrant:Collection = repo_ai_learning_v1

Mcp:Transport = stdio
```

Allow environment-variable overrides.

No cloud credential should be required for default local operation.

---

# 26. Recommended defaults

Use unless repository evidence requires otherwise:

```text
Temp hard lifetime                    24 hours
Temp cleanup interval                 60 minutes

Short-term review suggestion          3 days
Short-term compaction suggestion      7 days
Short-term archive suggestion         14 days

Long-term independent confirmations   2

Ollama                                http://localhost:11434
Embedding model                       embeddinggemma

Qdrant gRPC                           localhost:6334
Qdrant REST                           localhost:6333
Qdrant collection                     repo_ai_learning_v1

Semantic max results                  10
```

These are suggestions, not mandatory deletion thresholds for short-term memory.

Short-term deletion/compaction remains reviewable by the agent.

---

# 27. Background services

Implement hosted background services only for deterministic maintenance.

Allowed automatic work:

- temp expiration cleanup;
- index health/rebuild;
- archive rotation;
- processing explicitly approved archive candidates;
- pending embedding retries;
- Qdrant reconciliation.

Do not let background services autonomously:

- invent semantic summaries;
- delete uncertain short-term knowledge;
- validate long-term learning;
- resolve contradictions;
- override approval requirements.

---

# 28. Repository identity

Every memory record must have:

```text
repositoryId
```

Default recall stays inside the current repository.

Avoid cross-repository contamination.

Prefer explicit configured repository identity.

If deriving automatically, use stable repository metadata and do not rely solely on absolute machine path.

---

# 29. Parent/sub-agent integration

Support this flow:

```text
Parent Agent
    |
    +--> delegates parent_step_id
            |
            v
        Sub-agent
            |
            +--> work
            +--> status/evidence
            +--> optional learning signal
                        |
                        v
                 memory_remember
                        |
            temp or short-term first
                        |
                future review/outcome
                        |
             compact/delete/promote
                        |
                 long-term candidate
                        |
                     validate
                        |
                  semantic recall
```

Example learning signal:

```yaml
learning:
  candidate: true
  preferred_tier: short
  category: troubleshooting
  decision_area: persistence
  signal: "Verified reusable observation"
  evidence_refs:
    - "test result"
    - "file/symbol"
```

Sub-agents should usually write raw recent learning to temp or short-term first.

Long-term should be selective.

---

# 30. Memory review before user interruption

Agent behavior should support:

```text
encounter gap
   |
inspect current repository evidence
   |
recall relevant temp/short/long memory
   |
reconcile with current authority
   |
continue automatically when safe
   |
ask user only for a genuinely material unresolved decision
```

Memory cannot substitute for an explicit approval required by repository policy.

---

# 31. Suggested project structure

Adapt to existing architecture.

If no equivalent structure exists:

```text
src/
├── AiLearning.Mcp/
├── AiLearning.Application/
├── AiLearning.Domain/
└── AiLearning.Infrastructure/

tests/
├── AiLearning.UnitTests/
└── AiLearning.IntegrationTests/
```

Possible responsibility split:

## Domain

- memory record models;
- tiers;
- lifecycle;
- review state;
- outcomes;
- promotion rules;
- confidence rules;
- compaction invariants.

## Application

- remember;
- recall;
- review;
- delete;
- compact;
- archive;
- promote;
- retire;
- supersede;
- outcomes;
- cleanup;
- reindex.

## Infrastructure

- temp filesystem store;
- short-term filesystem store;
- archive store;
- canonical long-term store;
- file indexes;
- Qdrant;
- Ollama;
- event journal;
- content policy.

## MCP Host

- MCP tools;
- DTO validation;
- transport;
- DI composition;
- health/status.

Do not force this split if the repository already has a better architecture.

---

# 32. Docker Compose

Create:

```text
docker-compose.ai-learning.yml
```

for Qdrant.

Persist Qdrant data under:

```text
AiLearning/.vector/qdrant/
```

Bind only localhost ports.

Include health check if practical.

Assume Ollama can run natively on the laptop.

Document:

```bash
ollama pull embeddinggemma
```

---

# 33. Startup scripts

If repository is Windows-oriented, provide:

```text
scripts/start-ai-learning.ps1
scripts/stop-ai-learning.ps1
scripts/check-ai-learning.ps1
```

Optionally add shell equivalents.

Startup/check tooling should report:

- AiLearning folder accessibility;
- temp/short indexes;
- Qdrant;
- Ollama;
- embedding model;
- MCP server command.

Do not install system-wide dependencies without user consent.

---

# 34. Tests

Implement real tests.

## Temp filesystem memory

Test:

- file creation;
- retrieval;
- metadata filtering;
- update;
- promotion;
- explicit deletion;
- expiration;
- hard 24-hour cap;
- cleanup;
- process restart preserving unexpired temp files;
- expired temp not returned after restart;
- concurrent writes;
- index rebuild.

Important:

Temporary memory is file-based and may survive MCP process restart, but it must never survive beyond its persisted 24-hour maximum lifetime.

## Short-term filesystem memory

Test:

- file persistence across restart;
- review state;
- mark keep;
- delete;
- archive;
- semantic compaction commit workflow;
- source references;
- gzip archive creation;
- archive verification before source deletion;
- retrieve specific archived memory;
- index rebuild;
- deduplication;
- concurrent mutation;
- idempotent operations.

## Long-term

Integration-test:

- candidate creation;
- canonical record;
- embedding generation;
- Qdrant indexing;
- semantic retrieval;
- metadata filtering;
- outcome recording;
- promotion;
- supersession;
- retirement;
- Qdrant rebuild.

Create a paraphrase retrieval test, not merely exact text matching.

## Lifecycle

Test:

```text
short
-> long candidate
-> first successful independent confirmation
-> still candidate
-> second independent confirmation
-> validated
```

## Security

Test:

- path traversal;
- arbitrary file write rejection;
- secret filtering;
- oversized input;
- malformed MCP requests.

## Dependency failure

Test:

- Ollama offline;
- Qdrant offline;
- pending embedding;
- recovery;
- reindex.

---

# 35. Acceptance criteria

Implementation is complete only when these are actually demonstrated.

## AC1 — MCP

Server starts and an MCP client can enumerate/call tools.

## AC2 — Temp filesystem

Creating temp memory creates a record beneath `AiLearning/temp/`.

## AC3 — Temp restart behavior

Restarting the MCP server does not delete an unexpired temp file.

## AC4 — Temp 24-hour behavior

A temp record past its persisted 24-hour maximum is excluded from recall and removed by cleanup.

## AC5 — Short-term filesystem

Short-term memory exists as inspectable files beneath `AiLearning/short-term/`.

## AC6 — Agent review

An agent can list review candidates, inspect, mark, delete, archive, and promote short-term records.

## AC7 — Compaction

Multiple short-term records can be safely compacted into one reviewed summary while preserving source references.

## AC8 — Archive compression

Originals can be written to verified `.jsonl.gz` archive before active copies are removed.

## AC9 — Long-term canonical

Long-term candidate/validated memory creates canonical records.

## AC10 — Local embedding

Ollama successfully generates embeddings locally.

## AC11 — Qdrant

Long-term records are indexed in local Qdrant.

## AC12 — Semantic recall

A paraphrased query retrieves the appropriate long-term memory.

## AC13 — Feedback

Outcome recording updates usage/reliability metadata.

## AC14 — Reindex

Deleting/recreating Qdrant and running reindex rebuilds semantic memory entirely from canonical files.

## AC15 — Security

The MCP server cannot write outside `AiLearning/`.

## AC16 — Secret safety

Secret-bearing content is blocked/redacted before file persistence and embeddings.

## AC17 — Degraded mode

Ollama/Qdrant outages do not destroy temp/short/canonical long-term memory.

## AC18 — Concurrency

Parallel agent operations do not corrupt files/indexes or create uncontrolled duplicates.

## AC19 — Tests

Applicable unit/integration tests pass.

## AC20 — Evidence

Final report contains actual executed commands and actual results.

---

# 36. Required end-to-end verification

Before declaring completion:

1. build the relevant projects;
2. run unit tests;
3. create temp memory;
4. verify temp file exists;
5. restart MCP server;
6. verify unexpired temp memory remains;
7. simulate/create an expired temp record;
8. verify it is excluded and cleaned;
9. create short-term memory;
10. restart server;
11. verify short-term memory survives;
12. list short-term review candidates;
13. compact several test records;
14. verify compacted record references originals;
15. archive originals;
16. verify `.jsonl.gz` can be read;
17. create a long-term candidate;
18. start/verify Ollama;
19. generate local embedding;
20. start/verify Qdrant;
21. index the record;
22. perform paraphrased semantic recall;
23. record an outcome;
24. exercise promotion validation;
25. rebuild filesystem indexes;
26. rebuild Qdrant from canonical records;
27. inspect resulting `AiLearning/` tree;
28. run repository-specific instruction/diff checks.

Do not claim a verification step passed if it was not actually executed successfully.

Clearly distinguish:

```text
implemented
unit-tested
integration-tested
end-to-end verified
not verified
```

---

# 37. Final implementation report

At completion report:

```text
Architecture implemented
Projects/files created
MCP tools implemented

Temp filesystem design
Temp cleanup/24h enforcement

Short-term filesystem design
Review workflow
Deletion workflow
Compaction workflow
Archive compression workflow

Long-term canonical design
Embedding provider/model
Qdrant collection/index design

Security controls
Concurrency controls
Degraded-mode behavior

Tests executed
Actual test results
E2E verification
Known limitations
Remaining work
```

Also provide exact commands to:

```text
start Qdrant
start/check Ollama
pull embeddinggemma
start MCP server
connect MCP client
inspect memory folders
list review candidates
run cleanup
run compaction workflow
archive short-term memory
rebuild filesystem indexes
rebuild vector index
run tests
reset local runtime indexes safely
```

---

# 38. Design principles

Prefer:

```text
filesystem transparency
+ atomic writes
+ explicit review
+ bounded temporary retention
+ agent-curated short-term memory
+ auditable compaction
+ durable canonical learning
+ rebuildable vector indexes
+ local embeddings
+ deterministic lifecycle rules
```

over:

```text
hidden databases for everything
+ permanent raw memory
+ silent deletion
+ silent promotion
+ opaque self-learning
+ vector DB as source of truth
+ storing every agent observation forever
```

The objective is not maximum memory.

The objective is a local, inspectable, autonomous memory system where:

- temporary working memory disappears within 24 hours;
- short-term memory can be reviewed and curated by agents;
- short-term memory can be deleted, compacted, or archived safely over time;
- only durable reusable learning reaches long-term semantic memory;
- all durable learning remains auditable;
- semantic indexes are always rebuildable;
- repository authority remains stronger than learned memory.

Begin by inspecting the repository and existing `AiLearning` contracts, then implement the complete solution autonomously.
