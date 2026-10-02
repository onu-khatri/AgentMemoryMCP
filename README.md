# AgentMemoryMCP

A .NET 10 stdio MCP server for restart-safe agent sessions, multi-agent coordination, tiered filesystem memory, local Ollama embeddings and repository-isolated Qdrant recall.

## Build and run

```powershell
dotnet restore AgentMemoryMCP.slnx
dotnet build AgentMemoryMCP.slnx -c Release --no-restore
$env:Repository__Id = 'agent-memory-mcp'
dotnet run --project AgentSession.MCP/AgentSession.MCP.csproj -c Release --no-build
```

`Repository__Id` is required and must be a stable lowercase ID containing letters, digits and single hyphens. Moving a checkout should preserve its ID. Different repositories need different IDs; worktrees share sessions and learning only when they intentionally share an ID.

Runtime storage defaults to `%USERPROFILE%/.codex/AgentMemory` and can be changed with the absolute local `SystemStorage__Root` setting. Sessions use `sessions/<repository-id>/<session-id>/coordination`; canonical learning uses `repositories/<repository-id>/AiLearning`; Qdrant data uses `.vector/qdrant`. The checkout's [AiLearning](AiLearning/README.md) directory contains documentation and schemas only.

## Tools

Shared-session tools let agents discover or activate sessions, append revision-controlled structured artifacts, coordinate leased/fenced tasks, resume consistent paginated snapshots, and explicitly checkpoint consumed work. They operate without Ollama or Qdrant.

The 20 `memory_*` tools provide status, temp/short/long-candidate creation, get/update/recall, review, prepare/commit compaction, verified archives, explicit deletion, observation events, outcomes, promotion, supersession, retirement, cleanup and repository-scoped reindexing. Files remain canonical; vectors and filesystem indexes are derived. Semantic recall uses local `embeddinggemma` through Ollama and an isolated Qdrant collection, with lexical degraded behavior during dependency outages.

Agents should treat the server as their primary persisted memory source: activate and fully resume the shared session before repeating work, use focused `memory_recall` queries for reusable learning, write session progress with `append_agent_memory`, and write cross-session learning with `memory_remember`. `coordinate_agent_task` alone changes task ownership/state, and `checkpoint_agent_session` only acknowledges a fully consumed snapshot. This keeps prompt context bounded while preserving restart and multi-agent continuity. Current code, tests, repository instructions and explicit user decisions remain authoritative over recalled advice.

See the [developer onboarding wiki](wiki/Dev/README.md), [agent and skill wiki](wiki/agent-ai-guidence/README.md), [MCP contract](AiLearning/MCP-CONTRACT.md), [parent/sub-agent example](AiLearning/PARENT-SUBAGENT-EXAMPLE.md), and [operations guide](AiLearning/OPERATIONS.md).

## Logging and OpenTelemetry

The server writes bounded structured JSON logs to stderr and keeps stdout protocol-only. Remote OTLP export is off by default. Start the disposable loopback collector with `./scripts/Start-AgentMemoryObservability.ps1 -Mode Local`, inspect health/queue/drop evidence with `./scripts/Check-AgentMemoryObservability.ps1 -Mode Local`, and stop it with `./scripts/Stop-AgentMemoryObservability.ps1 -Mode Local`. The local sink is tmpfs and is removed with the container.

The server consumes `OnuObservability.Hosting` and `OnuObservability.Mcp` `0.1.0-alpha.9`; reusable observability implementation and reference documentation are owned by the sibling [OnuObservability repository](../OnuObservability/README.md). AgentMemory-specific compatibility settings, application descriptors, dashboards/SLOs, runbooks, rollback, and verification commands are in the [service observability guide](docs/observability/README.md). Disabling remote export with `Observability__ExporterEnabled=false` preserves MCP behavior, local instrumentation, and safe stderr diagnostics.

## Local semantic services

The provided scripts do not install dependencies:

```powershell
$root = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex/AgentMemory'
ollama pull embeddinggemma
./scripts/Start-AgentMemoryQdrant.ps1 -SystemRoot $root
./scripts/Test-AgentMemoryDependencies.ps1 -SystemRoot $root
```

Qdrant binds to loopback and persists in the central root. Configuration, backup/restore, model migration/rollback, archive recovery and repository-scoped reset procedures are in the operations guide.

## Contract and trust boundaries

Each MCP tool uses typed camelCase JSON and snake_case enum values. Mutation operation IDs are replay-safe; revision conflicts require reread and explicit reconciliation. Protected removal and high-risk validation require external operator-managed grants scoped to repository, record revision and action. MCP tools cannot create those grants.

Memory is advisory. Current code, tests, repository instructions, approved plans and explicit user decisions remain authoritative. Store concise facts, evidence and rationale, never credentials or private chain-of-thought. Actor IDs are local provenance rather than authentication, and task claims cannot control side effects performed outside this server.

Legacy freeform append and generic artifact/final-plan tools were removed because they could not express the required structured coordination rules. Startup does not copy, rewrite or delete historical data. See [legacy reuse and rollback](AiLearning/LEGACY-REUSE.md).

The OpenSpec task list and acceptance report distinguish implemented, automated, live-integration and cross-platform evidence. Passing only unit tests does not establish production readiness.
