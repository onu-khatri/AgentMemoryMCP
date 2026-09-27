## 1. Contracts and configuration foundations

- [x] 1.1 Add central storage and stable repository identity options to the existing composition root; verify default `%USERPROFILE%/.codex/AgentMemory`, per-repository sessions/learning paths, environment overrides, invalid roots and missing identity with options tests.
- [x] 1.2 Add versioned tier, review, lifecycle, outcome, embedding/index-state and relationship models plus JSON schemas covering source sections 6-8/11/16/20; verify representative valid/invalid fixtures and JSON round trips.
- [x] 1.3 Define bounded typed memory DTOs, revision/idempotency fields and documented error codes in the existing Contracts folders; verify serialization and schema snapshots against the new structured requirements rather than preserving obsolete fields.
- [x] 1.4 Bind defaults, quotas, timeouts and policy options with startup validation and injectable time; verify 24h cap, finite result/input bounds and loopback-only provider configuration.

## 2. Contained persistence and recovery

- [x] 2.1 Implement managed root/path resolution for both central sessions and repository learning; verify traversal, absolute paths, separators, reserved names, ADS, junction/symlink escapes and cross-repository IDs are rejected without outside writes.
- [x] 2.2 Extend atomic filesystem operations with UTF-8 validation, flush, safe staging cleanup and cancellation; verify injected failures leave either the previous or complete new record.
- [x] 2.3 Add cross-process repository locking and bounded keyed locks covering both session and learning writes; verify two-process contention, cancellation and revision conflicts without deadlock or lost updates.
- [x] 2.4 Add durable operation intents, commit visibility and recovery for multi-file mutations; verify restart at each commit boundary recovers one coherent result with idempotent replay.
- [x] 2.5 Implement canonical record indexes, normalized hashes and context-scoped deduplication; verify rebuild from files and concurrent identical creation without merging distinct claims.
- [x] 2.6 Implement sanitized append-oriented events, replay IDs and rotation; verify partial final-line recovery, no raw content in deletion events and no duplicate logical events after retries.

## 3. Content policy and selective reuse

- [x] 3.1 Implement recursive content validation across retained/new tool content, evidence, metadata, logs and embedding payloads; verify synthetic secrets never reach any sink.
- [x] 3.2 Record a retain/adapt/remove matrix for existing tools, stores and dependencies with requirement/test references; verify no retained surface exists solely for backward compatibility.
- [x] 3.3 Adapt useful session discovery/activation, plan and output capabilities to central structured storage; verify resume and repository isolation through supported contracts.
- [x] 3.4 Remove or replace obsolete freeform coordination, unsafe overwrites and cancellation suppression; verify unsupported old payloads fail clearly and tests assert new behavior.
- [x] 3.5 Publish new client configuration, breaking contract/path changes and rollback limitations; verify startup leaves historical files untouched and requires no legacy migration.

## 4. Session resumption and multi-agent coordination

- [x] 4.1 Add structured context, decision, finding, task, handoff and checkpoint models under central session storage; verify versioned round trips and repository/session isolation.
- [x] 4.2 Extend append_agent_memory with atomic structured batches, actor/revisions/operation ID and structured response metadata; verify supported batches, unsupported freeform rejection, conflicting edits and retry recovery.
- [x] 4.3 Reserve coordination artifacts against generic overwrite and route task transitions through the coordination service; verify existing artifact tools cannot bypass revisions or task ownership.
- [x] 4.4 Implement task creation with stable work keys and atomic claim/renew/release using persisted leases and fencing tokens; verify two-process competing claims, deduplicated delegation and dependency checks.
- [x] 4.5 Implement expired takeover, block, complete and audited reopen with output/evidence/handoff commit; verify stale-owner rejection after restart and completed tasks cannot be reclaimed implicitly.
- [x] 4.6 Implement consistent resume snapshots, monotonic change sequences and bounded snapshot pagination; verify objective/plan/progress/outputs/owners/blockers/next steps and concurrent contributions without omitted changes.
- [x] 4.7 Implement per-agent explicit checkpoint acknowledgement and resynchronization for invalid cursors; verify reads do not advance checkpoints and restart preserves acknowledged progress.
- [x] 4.8 Expose resume_agent_session, checkpoint_agent_session and coordinate_agent_task with typed schemas and cancellation; verify real MCP calls and all primary workflows while Ollama/Qdrant are offline.
- [x] 4.9 Run SC1-SC6 end-to-end with two agents and a restarted server before semantic-learning work; retain evidence of resumed work, shared outputs, claim exclusion, expired takeover, checkpoint catch-up and safe contract transitions.

## 5. Temporary memory

- [x] 5.1 Implement temp remember/get/update/filter/recent recall/delete using canonical JSON and persisted timestamps; verify actual files, metadata filters, revisions and no embedding calls.
- [x] 5.2 Enforce earliest persisted/configured/24h expiry on every read and write path; verify exact boundary, earlier expiry, attempted extension, future timestamps and restart using controlled time.
- [x] 5.3 Implement startup, opportunistic and hosted expiration cleanup with index repair; verify repeated sweeps and restart exclude/remove expired content without deleting unexpired records.
- [x] 5.4 Implement recoverable temp-to-short promotion; verify source provenance, interruption/retry and one resulting short record.

## 6. Short-term curation and archives

- [x] 6.1 Implement persistent short remember/get/lexical listing, review filters and all review actions; verify restart persistence and age suggestions never cause semantic deletion.
- [x] 6.2 Implement explicit safe deletion and protected/audit-record policy checks with events; verify delete_candidate is non-destructive and protected removal fails without policy authorization.
- [x] 6.3 Implement JSONL+gzip archives and ID/hash manifests with bounded full read-back before source removal; verify corruption, decompression limits, crashes, retries and explicit archived lookup.
- [x] 6.4 Implement prepare/commit compaction and memory_compact convenience path for agent-supplied reviewed summaries; verify every source/evidence/contradiction is represented, stale revisions fail and originals archive by default.
- [x] 6.5 Implement reviewed duplicate merge through the compaction path with explicit relationship metadata; verify similar conflicting claims are not automatically merged and evidence is preserved.
- [x] 6.6 Add deterministic maintenance suggestions and processing of explicitly approved archive candidates; verify protected records and unreviewed knowledge remain intact.

## 7. Long-term learning and feedback

- [x] 7.1 Implement canonical candidate creation and short-to-candidate promotion with independent lifecycle/embedding/index state; verify outages cannot lose canonical records or alter validation state.
- [x] 7.2 Implement replay-safe outcomes, usage metadata and documented confidence formula; verify all six outcomes, repeated IDs, and independent-confirmation grouping.
- [x] 7.3 Implement explicit validation policy, two independent successes, optional authoritative verification and high-risk approval checks; verify first success remains candidate, second allows promotion, repeated actors/sessions do not count and high-risk auto-validation fails.
- [x] 7.4 Implement supersession, retirement and contradiction/staleness handling with canonical history; verify immediate ordinary-recall exclusion even with stale vectors and explicit historical retrieval.

## 8. Local embeddings and vector indexing

- [x] 8.1 Add a pinned official Qdrant.Client dependency and HttpClientFactory Ollama adapter; verify restore and adapter tests for single/batch embedding, cancellation, timeout, truncate=false and malformed/non-finite/dimension-mismatched responses.
- [x] 8.2 Implement embedding projection/fingerprint and repository-specific versioned collection/alias naming; verify same-dimensional different models and projection-version changes cannot mix vectors.
- [x] 8.3 Implement stable point IDs, payload metadata/filter indexes and revision-checked upsert; verify repository/status/category/tag filters against real isolated Qdrant collections.
- [x] 8.4 Implement durable pending work, bounded backoff and reconciliation for missing/orphan/stale vectors; verify offline creation and recovery without duplicate canonical records or cross-repository mutation.
- [x] 8.5 Implement scoped rebuild and model migration with snapshot/replay, completeness checks and recoverable alias switch; verify failed builds retain old collection and concurrent updates survive migration, including crash after switch.

## 9. Unified recall and MCP integration

- [x] 9.1 Implement tier-ordered bounded recall, recent empty query, all metadata filters, explicit archive/history access and canonical vector-hit verification; verify ordering, exclusions, minimum similarity and lexical/semantic score labeling.
- [x] 9.2 Implement degraded lexical fallback including eligible canonical long records and structured health reasons; verify temp/short remain usable during each dependency outage without fake semantic scores.
- [x] 9.3 Expose all 20 new memory tool names listed in memory-mcp-operations alongside justified retained session tools and three coordination tools using one registration path; verify discovery, schemas, annotations, valid calls and malformed requests through a real MCP subprocess.
- [x] 9.4 Upgrade the preview MCP SDK to a pinned stable compatible release after reviewing its migration guidance; verify supported old/new client initialization, supported structured payloads/results, cancellation and stdout purity.
- [x] 9.5 Implement status and bounded hosted cleanup/index/embedding/reconciliation jobs with shutdown cancellation; verify counts, last-run fields, degraded status and absence of autonomous semantic decisions.

## 10. Operations and release readiness

- [x] 10.1 Add loopback-only pinned Qdrant Compose using the resolved central vector-data path and idempotent start/stop/check scripts; verify configuration rendering, startup/check output and data persistence without automatic dependency installation.
- [x] 10.2 Add AiLearning README/INDEX/MCP-CONTRACT/schemas documentation, parent/sub-agent learning examples and client configuration; verify every command uses central storage/repository identity and documents advisory memory/no hidden reasoning.
- [x] 10.3 Document backup/restore, model rollback, archive recovery and scoped reset commands; verify on isolated data without deleting shared collections, other repository records or canonical learning.
- [x] 10.4 Add CI fresh restore/build/unit/filesystem/protocol checks and separately reported real integration lane; verify missing dependencies are explicit skips/blockers rather than reported passes.
- [x] 10.5 Validate packaging with Release publish and launched-binary MCP smoke tests on Windows and a supported Unix runner; report exact build/test totals and any platform gaps.

## 11. Acceptance demonstration

- [x] 11.1 Execute source section 36 steps 1-16 in an isolated central root: build/tests, temp creation/restart/expiry, short restart/review/compaction/archive; retain command results and filesystem/archive evidence for AC1-AC8.
- [x] 11.2 Execute steps 17-26 with real Ollama embeddinggemma and isolated Qdrant: candidate/embedding/paraphrase/outcomes/validation and rebuild from canonical files; retain evidence for AC9-AC14 without substituting mocked vectors.
- [x] 11.3 Exercise containment, secret rejection, two-repository/two-process races, dependency outages and crash recovery; record actual results for AC15-AC18.
- [x] 11.4 Complete steps 27-28 using central storage paths, inspect resulting tree and scoped diff, and produce AC1-AC20 verification matrix with exact commands/totals, implemented/tested/E2E distinctions and unverified limitations; declare production readiness only after all required demonstrations pass.

- [x] 11.5 Re-run SC1-SC6 against the final integrated server and include primary session-continuity/coordination results in the release matrix; verify learning maintenance does not remove session progress and no readiness claim relies solely on semantic tests.

## Final implementation checkpoint (2026-09-27)

- Fresh Release restore/build succeeded with 0 warnings and 0 errors. The standard suite passed 150, failed 0 and explicitly skipped 3 infrastructure-gated tests (153 total). The separately enabled real Ollama/Qdrant lane passed 3/3 with no skips.
- Final SC1-SC6 verification passed 12/12, including restart-safe resume, shared outputs, two-process claims, stale-owner fencing, checkpoint catch-up, historical-file preservation and proof that learning cleanup does not remove session progress.
- Windows `win-x64` and Linux `linux-x64` self-contained executables launched and exposed 26 unique tools over clean stdio JSON-RPC. The Linux binary ran in the official .NET 10 runtime-dependencies container.
- AC1-AC20 and all 28 source verification steps are mapped to executed evidence in `verification.md`. The repository-local OpenSpec change validates strictly with no issues.
- Hosted GitHub Actions has not run because this working tree was not pushed. The workflow has separate Windows/Ubuntu verification and opt-in real semantic lanes; hosted status remains a release boundary and is not represented as passing evidence.
