# Glossary

[Previous: Mind map](21-project-mind-map.md) | [Developer wiki](README.md) | [Next: Readiness checklist](23-developer-readiness-checklist.md)

**Active collection** — The physical Qdrant collection currently targeted by a stable repository alias.

**Actor ID / Agent ID** — A local provenance identifier supplied by a caller. It is not authentication.

**Advisory memory** — Remembered content that can guide work but cannot override current source, tests, instructions, approvals or user decisions.

**Archive** — A bounded gzip-compressed JSONL package plus manifest, verified before active source removal.

**Artifact** — A revision-controlled session record of kind context, decision, finding, handoff, plan or output.

**Canonical record** — The persisted filesystem record treated as recoverable truth for learning.

**Catalog** — A sharded, hash-verified derived index of canonical memory identity, path, tier, state, revision and hashes.

**Checkpoint** — One actor's persisted acknowledgement that it consumed a complete resume snapshot sequence.

**Claim token** — Secret-like fencing value returned to the current task owner and required for owner transitions and task-linked writes.

**Compaction** — Agent-authored consolidation of reviewed short records with complete source/evidence/contradiction coverage and verified archival.

**Content hash** — Normalized hash of memory content and structured data used for integrity and vector freshness.

**Degraded recall** — Lexical/canonical recall used when semantic dependencies are unavailable or incompatible, with health reasons disclosed.

**Derived state** — Data rebuildable from canonical records, notably catalog shards, embeddings and Qdrant points.

**Embedding fingerprint** — Hash of provider, model, digest, dimension and projection version that prevents incompatible vector mixing.

**Evidence reference** — Opaque string naming proof or output. The server stores but does not dereference or verify it.

**Fencing generation** — Monotonically increasing task-claim generation that distinguishes takeovers.

**Handoff** — Structured session artifact or completion field describing finished work, blockers and next actions for another agent.

**Intent** — Durable transaction file declaring that a multi-file commit must roll forward to completion.

**Learning tier** — Temp, short or long canonical storage class with distinct lifecycle rules.

**Lease** — Server-time ownership interval on a coordinated task.

**Long candidate** — Proposed long-term learning awaiting validation policy and explicit promotion.

**Managed mutation** — One expected-hash-checked file write or deletion inside a transaction.

**Operation ID** — Stable identifier for one logical mutation across retries.

**Operation receipt** — Persisted request hash/result identity proving a logical mutation already committed.

**Ordinary recall** — Default eligible-memory search excluding candidates and historical/inactive states.

**Outcome** — Evidence-backed record describing how durable learning performed in a session/task.

**Pending vector work** — Durable instruction to embed, update or remove a long-memory vector later.

**Plan reference** — Artifact ID in shared context explicitly selecting the current plan.

**Projection** — Deterministic text representation of a long record sent to the embedding model.

**Protected record** — Memory whose destructive archive/delete or high-risk validation requires an operator grant.

**Repository identity** — Stable startup-bound ID isolating all sessions, learning and vector filtering for one repository.

**Repository lock** — Combined in-process semaphore and exclusive OS file lease serializing managed reads/recovery/writes.

**Revision** — Monotonic record version used for optimistic concurrency.

**Review action** — Explicit short-memory decision: keep, compact candidate, promotion candidate, archive candidate or delete candidate.

**Semantic score** — Qdrant vector similarity for a verified hit; it is not truth/confidence.

**Session** — Repository-scoped durable boundary for one coherent body of operational work.

**Snapshot** — Immutable, actor-owned, expiring view of session state split into hash-verified chunks.

**Source coverage** — Compaction mapping proving each source's evidence and unresolved contradictions are represented.

**Stale claim** — Expired/replaced ownership token that cannot modify task-owned state.

**Supersede** — Replace active long-term learning with a named canonical successor while preserving history.

**Retire** — Remove long-term learning from ordinary recall without physical deletion.

**Tool annotation** — MCP hint describing read-only, destructive, idempotent or open-world behavior; not enforcement.

**Validated learning** — Long-term candidate that met policy and explicit promotion requirements; still advisory.

**Vector alias** — Stable Qdrant name atomically switched between compatible physical collections.

**Work key** — Stable semantic identity used to deduplicate delegated tasks inside a session.

