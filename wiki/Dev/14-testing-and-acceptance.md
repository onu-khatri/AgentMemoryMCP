# Testing architecture and acceptance evidence

[Previous: Maintenance](13-hosted-maintenance-and-status.md) | [Developer wiki](README.md) | [Next: Production source catalog](15-production-source-catalog.md)

## Test layers

| Layer | Examples | What it proves |
| --- | --- | --- |
| Pure/unit | hashing, projection, record validation | deterministic local invariants |
| Filesystem integration | temp roots, archives, transactions | real persistence and recovery |
| Service integration | session coordination, curation | business workflow across services |
| MCP subprocess | `McpProcess`, `McpSessionTests` | real stdio negotiation, discovery, schemas and calls |
| Multi-process | `WorkerProcess`, TestWorker | OS locking and crash boundaries |
| Live semantic | Qdrant/Ollama-gated tests | real embeddings, filters, recall and migration |
| Publish smoke | PowerShell + native binaries | packaging, protocol purity and cross-platform launch |

## Major test files

- `AtomicFileTests` verifies atomic replacement and staging cleanup.
- `ManagedStoragePathTests` verifies containment and link/path attacks.
- `ManagedTransactionTests` injects faults at durable boundaries and verifies roll-forward recovery.
- `RepositoryMutationLockTests` verifies cross-process serialization and cancellation.
- `SessionCoordinationTests` covers artifacts, claims, expiry takeover, snapshots and checkpoints.
- `CanonicalMemoryTests` covers tiers, expiry, curation, archives, lifecycle, outcomes and recall.
- `MemoryContentPolicyTests` covers direct, structured and encoded secrets.
- `MemoryContractTests` and `MemoryRecordTests` cover schemas, serialization and invariants.
- `OllamaEmbeddingClientTests` covers cardinality, timeout, cancellation, truncation and malformed vectors.
- `EmbeddingProjectionTests` covers deterministic projection/fingerprints.
- `QdrantVectorIndexTests` includes mocked behavior and opt-in live end-to-end scenarios.
- `McpSessionTests` verifies all 26 tools through real protocol subprocesses.

## Time and failure injection

Services accept `TimeProvider`, enabling exact expiry, lease and review-boundary tests. `ManagedTransactionStore` exposes internal fault hooks under `InternalsVisibleTo` so tests can stop after intent, mutation or receipt boundaries. The test worker enables failures in a separate process.

## Live-test gating

The standard suite skips three tests unless `AGENT_MEMORY_RUN_QDRANT_TESTS=1`. Skips must remain visible and never be counted as passes. Live tests use isolated repository collections and real `embeddinggemma`.

## Acceptance sources

- [tasks.md](../../openspec/changes/archive/2026-09-27-productionize-agent-memory/tasks.md) lists implementation obligations.
- [session-verification.md](../../openspec/changes/archive/2026-09-27-productionize-agent-memory/session-verification.md) records SC1-SC6.
- [verification.md](../../openspec/changes/archive/2026-09-27-productionize-agent-memory/verification.md) records exact local and live results.
- [.github/workflows/ci.yml](../../.github/workflows/ci.yml) defines hosted Windows, Linux and semantic lanes.

## Adding tests for a change

Test the behavior at the lowest meaningful layer, then add protocol coverage if the tool contract changes. For durability changes, inject failures before and after the durable decision. For time behavior, use controlled time. For semantic changes, separate provider-adapter tests from real dependency evidence.

Avoid tests that only mirror implementation structure or repeat a passing scenario without a new failure boundary.
