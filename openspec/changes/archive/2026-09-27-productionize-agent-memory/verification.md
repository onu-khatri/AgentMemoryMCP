# Productionization verification

Date: 2026-09-27

Host: Windows, .NET SDK 10.0.401
Live dependencies: Ollama with `embeddinggemma:latest`; Qdrant 1.19.1 on loopback

This report separates local execution evidence from hosted state. The implementation and required acceptance demonstrations passed locally. The new GitHub Actions workflow has been parsed and its commands were exercised locally, but it has not run on GitHub because this working tree was not pushed during this change.

## Executed commands and results

| Command/check | Actual result |
| --- | --- |
| `dotnet restore AgentMemoryMCP.slnx --force-evaluate` | Succeeded for all three projects. The sandboxed first attempt could not read the user NuGet configuration; the approved retry succeeded. |
| `dotnet build AgentMemoryMCP.slnx -c Release --no-restore` | Succeeded with 0 warnings and 0 errors. The test worker is now built/copied in Release configuration. |
| `dotnet test AgentMemoryMCP.slnx -c Release --no-build --logger "trx;LogFileName=standard-Windows-acceptance.trx"` | 150 passed, 0 failed, 3 explicitly skipped, 153 total. The skipped tests are precisely the environment-gated Qdrant/Ollama tests. |
| Focused `ProtocolDiscoveryHasOnlyUniqueSupportedTools` run after the agent-guidance audit | 3 passed across protocol versions 2025-06-18, 2025-11-25 and 2026-07-28. All 26 tools expose a nonempty description with explicit usage and exclusion guidance; assertions also protect the session/learning, resume/checkpoint, review/lifecycle and filesystem/vector distinctions. |
| Full Release suite after the agent-guidance audit | 150 passed, 0 failed, 3 explicitly skipped, 153 total. No behavioral regression was introduced by the description, prompt, package metadata and documentation changes. |
| `AGENT_MEMORY_RUN_QDRANT_TESTS=1` plus the real-integration filter | 3 passed, 0 failed, 0 skipped. This used real `embeddinggemma:latest` and Qdrant 1.19.1. |
| Final SC1-SC6 filter | 12 passed, 0 failed, 0 skipped. |
| `dotnet publish ... -r win-x64 --self-contained true` plus `Test-PublishedMcp.ps1` | Published executable launched; 26 unique tools discovered; protocol 2025-11-25; stdout contained clean JSON-RPC. |
| `dotnet publish ... -r linux-x64 --self-contained true` plus the same smoke script in `mcr.microsoft.com/dotnet/runtime-deps:10.0` | Linux executable launched; 26 unique tools discovered; protocol 2025-11-25; stdout contained clean JSON-RPC. |
| `Test-AgentMemoryDependencies.ps1` | Vector storage writable; Qdrant REST healthy; Qdrant gRPC reachable; Ollama healthy; `embeddinggemma:latest` available. |
| Backup/restore/reset isolated fixture | Backup created and restored to a new root. Reset removed only repository-one derived indexes; repository-one canonical data and repository-two index data remained. |
| Isolated Qdrant persistence fixture | Compose rendered with loopback ports, Qdrant started, a probe collection survived container recreation, and the isolated project was stopped without deleting its bind data during the check. |
| Documentation/schema/script checks | Eight local documentation link sets validated, seven JSON schemas parsed, eight PowerShell scripts parsed, and workflow YAML parsed with jobs `verify` and `semantic-integration`. |
| Developer agent/skill wiki audit | 20 Markdown files under `wiki/agent-ai-guidence`; every page contains a real-life example, all local links resolve, all fenced JSON parses, Markdown fences are balanced, and the selection reference covers all 26 tools. |
| Developer implementation wiki audit | 24 Markdown files under `wiki/Dev`, 1,990 lines and 11,709 words; all local links resolve, fenced JSON parses, no line-number anchors are used, all 26 tools have individual Mermaid flows, every authored production/test-worker C# file is cataloged, and no trailing whitespace was found. |
| `openspec validate productionize-agent-memory --type change --strict --json` | 1 change passed, 0 failed, no issues. |
| `git diff --check` | No whitespace errors; only line-ending conversion warnings on Windows. |

TRX evidence is under `AgentSession.MCP.Tests/TestResults/` in the local working tree. Publish and tree-inspection artifacts are under the ignored `.tmp/` workspace and are disposable.

## Required 28-step sequence

| Steps | Evidence and status |
| --- | --- |
| 1-2 build and tests | **End-to-end verified locally.** Fresh restore, Release build and 153-test standard run above. |
| 3-8 temp create/file/restart/expiry/cleanup | **Integration-tested.** `RestartPreservesTempExpiryAndCleanupIsIdempotent`, `ServerStartupPhysicallySweepsExpiredTempWithoutGetCall`, exact-boundary/configured-lifetime tests and MCP restart tests use isolated central roots and inspect canonical files. |
| 9-12 short create/restart/review | **Integration-tested.** `EveryShortReviewActionPersistsAcrossRestartAndRemainsFilterable` covers all five actions; `MemoryToolsPersistRecallPromoteAndReviewAcrossRestart` executes the MCP path. |
| 13-16 compaction/archive/gzip read-back | **Integration-tested.** Compaction success/replay, stale/incomplete coverage, duplicate-merge, archive interruption/corruption and decompression-limit tests verify source relationships and `.jsonl.gz` read-back before removal. A real MCP prepare/commit round trip also passed. |
| 17 long candidate | **End-to-end verified with live dependencies.** The semantic integration suite creates canonical candidates before indexing. |
| 18-20 Ollama/embedding/Qdrant | **End-to-end verified.** Dependency health passed and live tests discovered the model digest, generated 768-dimensional finite embeddings and used isolated repository collections. |
| 21-22 index/paraphrased recall | **End-to-end verified.** Live migration/reconciliation indexed current revisions; the paraphrase `How should retries for local services be controlled?` retrieved the bounded-retry record as a semantic hit. |
| 23 outcome | **End-to-end verified.** Live outcome writes changed canonical revision and the stale vector was reconciled. Standard tests cover all six outcome kinds and replay. |
| 24 validation | **End-to-end verified.** Two live independent successes with distinct sessions, actors and evidence promoted a candidate to `validated`, then its new revision was indexed. High-risk grant enforcement remains covered by filesystem tests because grants are operator inputs. |
| 25 filesystem rebuild | **Integration-tested.** Derived indexes were removed/rebuilt from canonical records; invalid records remain reported and untouched. |
| 26 Qdrant rebuild | **End-to-end verified.** Migration built a fresh compatible collection from canonical records, replayed a concurrent outcome, switched the alias only after verification, retained the old collection and recovered a simulated crash after switch. |
| 27 inspect runtime tree | **End-to-end verified.** An isolated published server produced 1 temp, 1 short, 1 long candidate and 1 pending embedding under `<root>/repositories/acceptance-tree/AiLearning`. Inspection showed canonical tier files, sharded indexes, pending work, events and operation receipts. |
| 28 repository checks | **Verified.** No repository `AGENTS.md` exists, OpenSpec strict validation passed, local documentation links/schemas/scripts passed, and scoped status/diff checks preserved unrelated staged and untracked work. |

## AC1-AC20 matrix

| AC | Status | Primary evidence |
| --- | --- | --- |
| AC1 MCP | **E2E verified** | Three protocol versions enumerate unique typed tools; Windows/Linux published-binary smokes discover 26 tools. |
| AC2 Temp filesystem | **E2E verified** | Isolated tree contains `temp/sessions/...json`; canonical temp tests inspect files. |
| AC3 Temp restart | **E2E verified** | Unexpired temp survives process/service reconstruction with unchanged expiry. |
| AC4 Temp 24-hour | **Integration-tested** | Hard/configured expiry, hidden reads, startup/opportunistic cleanup and idempotent sweeps pass with injected time. |
| AC5 Short filesystem | **E2E verified** | Isolated tree contains `short-term/active/...json`; restart tests read it. |
| AC6 Agent review | **E2E verified** | MCP get/review plus all review states, explicit deletion, archive and promotion tests pass. |
| AC7 Compaction | **E2E verified** | Real MCP prepare/commit plus service fault/replay tests preserve full source/evidence/contradiction coverage. |
| AC8 Archive compression | **Integration-tested** | Verified bounded gzip read-back/manifests precede source removal; corruption preserves sources. |
| AC9 Long canonical | **E2E verified** | Live candidate files persist independently of vector state. |
| AC10 Local embedding | **E2E verified** | Real Ollama `embeddinggemma:latest` generated and fingerprinted vectors. |
| AC11 Qdrant | **E2E verified** | Real repository-scoped points, payload filters and revision checks pass. |
| AC12 Semantic recall | **E2E verified** | Real paraphrased query returns the intended canonical record with semantic match type. |
| AC13 Feedback | **E2E verified** | Live outcome changes revision and reindex state; deterministic confidence/replay tests pass. |
| AC14 Reindex | **E2E verified** | Fresh versioned collection rebuilt from canonical files, verified and alias-switched; old collection retained. |
| AC15 Security | **Integration-tested** | Root/repository containment rejects traversal, rooted paths, ADS, devices and reparse links before writes. Shared vector/lock paths are the documented central-root exceptions. |
| AC16 Secret safety | **Integration-tested** | Direct, nested JSON, URL/base64, authorization, connection-string and private-key fixtures are rejected before canonical/event/pending/embedding sinks. |
| AC17 Degraded mode | **E2E verified** | Real Ollama-offline and Qdrant-offline recall returns labeled lexical results while canonical data remains intact. |
| AC18 Concurrency | **Integration-tested** | Two-process locks, claims, revision conflicts, equivalent-create deduplication and crash-boundary recovery pass. |
| AC19 Tests | **Verified** | 150 standard passes plus 3/3 separately enabled live passes; infrastructure skips are reported explicitly. |
| AC20 Evidence | **Verified** | This report records commands, exact totals, limits and hosted-state distinction. |

## SC1-SC6 matrix

| Scenario | Status | Evidence |
| --- | --- | --- |
| SC1 restart-safe resume | **E2E verified** | Structured objective, plan references, outputs, unknown fields and next actions survive real subprocess restart. |
| SC2 shared contributions | **E2E verified** | Two agents observe completed outputs/evidence; revision conflicts cannot partially commit. |
| SC3 claims and takeover | **E2E verified** | Two-process claim exclusion, persisted lease, explicit expiry takeover and stale-token rejection pass. |
| SC4 checkpoint/catch-up | **E2E verified** | Consistent pagination, concurrent later contribution, explicit checkpoint and restart catch-up pass. |
| SC5 semantic independence | **E2E verified** | Session scenarios run without semantic services; `LearningCleanupDoesNotRemoveDurableSessionProgress` proves temp cleanup leaves session context intact. |
| SC6 contract transition | **E2E verified** | Unique supported tools work across three protocol versions, obsolete generic/freeform paths fail, and historical files remain byte-identical. |

## Remaining release boundary

No required local acceptance scenario is failing. Hosted GitHub Actions state is **not verified** because no branch was pushed or workflow dispatched in this task. The workflow defines Windows and Ubuntu fresh restore/build/test/publish/smoke jobs and a separate opt-in real Ollama/Qdrant lane; its first hosted run remains a release gate rather than being represented as a pass here.
