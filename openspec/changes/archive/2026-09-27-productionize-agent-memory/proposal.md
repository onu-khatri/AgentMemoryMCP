## Why

The primary purpose is to let an agent resume a session where it left off and let multiple agents coordinate in the same session without repeating completed work. Extend the existing .NET 10 session/artifact server with structured shared context, checkpoints, discoverable outputs and exclusive task claims; the memory tiers and local semantic learning support these outcomes without being prerequisites for session continuity.

## What Changes

- Make session resumption and multi-agent coordination first-class: objective/plan, decisions, completed work and evidence, task owners, blockers, handoffs, next steps and changes since each agent's checkpoint.
- Extend `append_agent_memory` for structured artifact updates, revision checks and replay protection; retain old behavior only when required by the new workflows. Add atomic task claims, renewal/release, expiry and stale-owner protection. Session coordination artifacts remain durable independently of learning-tier retention.

- Add filesystem temporary memory with a persisted maximum 24-hour lifetime; persistent short-term review, deduplication, compaction, and verified gzip archives; canonical long-term candidates, validation, feedback, supersession, and retirement.
- Add local Ollama embeddings and rebuildable Qdrant indexing, repository-scoped recall, degraded operation, reconciliation, and controlled model migration.
- Add typed `memory_*` tools, versioned JSON schemas, deterministic background maintenance, health reporting, content policy, bounded inputs, cancellation, multi-process coordination, and recoverable multi-file operations.
- **BREAKING**: use `%USERPROFILE%/.codex/AgentMemory/sessions/<repository-id>/` for sessions and `repositories/<repository-id>/AiLearning/` under the same root for learning. Replace obsolete payloads/storage behavior as needed; compatibility with all ten old tools, freeform append, YAML/Markdown persistence and automatic legacy import are not requirements. Existing user data must remain untouched unless an explicit import/removal is authorized.
- Reuse existing code only where it meets the new requirements. Preserve useful session discovery, plan/evidence and artifact concepts; consolidate redundant tools and replace unsafe or unstructured behavior. Document retained/replaced/removed capabilities and client contract changes.
- Retain the existing project structure, .NET 10, host, DI, logging, and xUnit. Upgrade the preview MCP SDK through protocol compatibility tests; add production configuration, local infrastructure scripts, documentation, and release verification.

## Capabilities

### New Capabilities

- `session-coordination`: restart-safe resumption, shared structured artifacts, per-agent checkpoints, discoverable completed work, task claims and handoffs.

- `memory-storage-safety`: repository identity, contained storage, secret policy, concurrency, recovery, indexes, and audit.
- `session-storage-migration`: central session storage, selective reuse and safe handling of historical data; automated legacy migration is deferred unless a concrete need is confirmed.
- `temporary-memory`: persisted TTL, CRUD, filtering, cleanup, and promotion.
- `short-term-curation`: review, compaction, archive integrity, deletion, and retention policies.
- `long-term-learning`: candidates, evidence-based validation, outcomes, confidence, supersession, and retirement.
- `semantic-memory-recall`: local embeddings, Qdrant, unified retrieval, outages, rebuild, and model migration.
- `memory-mcp-operations`: MCP surface, options, health, maintenance, deployment, and acceptance evidence.

### Modified Capabilities

None. `openspec list --specs` reports no existing specs; the current session behavior is captured by a new compatibility specification.

## Impact

Extend `AgentSession.MCP/{Contracts,Models,Interfaces,Services,Options,Tools,Extensions}` and host composition. Extend `AgentSession.MCP.Tests` and add explicit infrastructure-backed tests. Add repository-maintained `AiLearning` documentation/schemas for the central runtime layout, `docker-compose.ai-learning.yml`, PowerShell operations scripts, and CI. Introduce `Qdrant.Client` and an HttpClientFactory-based Ollama adapter; filesystem records remain canonical, with no SQLite store. The proposal includes all 20 acceptance criteria and the source prompt's end-to-end demonstration, but this planning change does not implement them.

Primary acceptance additionally requires a stopped agent to resume from persisted context and two agents to coordinate one session without duplicate claims or lost contributions, including when Ollama/Qdrant are offline.
