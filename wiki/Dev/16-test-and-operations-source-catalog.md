# Test and operations source catalog

[Previous: Production catalog](15-production-source-catalog.md) | [Developer wiki](README.md) | [Next: Technology decisions](17-technology-and-design-decisions.md)

## Test infrastructure

| File | Responsibility |
| --- | --- |
| [McpProcess.cs](../../AgentSession.MCP.Tests/McpProcess.cs) | Starts the real server, negotiates MCP and sends JSON-RPC/tool calls |
| [WorkerProcess.cs](../../AgentSession.MCP.Tests/WorkerProcess.cs) | Starts the separate test worker for cross-process/fault scenarios |
| [TestWorker Program.cs](../../AgentSession.MCP.TestWorker/Program.cs) | Executes isolated lock/transaction commands in another process |

## Test suites

| File | Focus |
| --- | --- |
| [AtomicFileTests.cs](../../AgentSession.MCP.Tests/AtomicFileTests.cs) | atomic write semantics and staging cleanup |
| [ManagedStoragePathTests.cs](../../AgentSession.MCP.Tests/ManagedStoragePathTests.cs) | root validation, traversal, ADS, device names and link escapes |
| [ManagedTransactionTests.cs](../../AgentSession.MCP.Tests/ManagedTransactionTests.cs) | intent/receipt boundaries, roll-forward and idempotency |
| [RepositoryMutationLockTests.cs](../../AgentSession.MCP.Tests/RepositoryMutationLockTests.cs) | two-process contention, timeout and cancellation |
| [SessionCoordinationTests.cs](../../AgentSession.MCP.Tests/SessionCoordinationTests.cs) | context/artifacts/tasks/claims/snapshots/checkpoints and SC scenarios |
| [CanonicalMemoryTests.cs](../../AgentSession.MCP.Tests/CanonicalMemoryTests.cs) | tiers, expiry, review, archive, compaction, lifecycle, outcomes and recall |
| [MemoryConfigurationTests.cs](../../AgentSession.MCP.Tests/MemoryConfigurationTests.cs) | binding and fail-fast option validation |
| [MemoryContentPolicyTests.cs](../../AgentSession.MCP.Tests/MemoryContentPolicyTests.cs) | nested/direct/encoded secret rejection |
| [MemoryContractTests.cs](../../AgentSession.MCP.Tests/MemoryContractTests.cs) | request schemas, bounds and serialization contracts |
| [MemoryRecordTests.cs](../../AgentSession.MCP.Tests/MemoryRecordTests.cs) | model round trips and invalid persisted-state rejection |
| [EmbeddingProjectionTests.cs](../../AgentSession.MCP.Tests/EmbeddingProjectionTests.cs) | deterministic projection and fingerprint compatibility |
| [OllamaEmbeddingClientTests.cs](../../AgentSession.MCP.Tests/OllamaEmbeddingClientTests.cs) | adapter validation, timeout and cancellation |
| [QdrantVectorIndexTests.cs](../../AgentSession.MCP.Tests/QdrantVectorIndexTests.cs) | vector filters, reconciliation, migration and opt-in live dependencies |
| [McpSessionTests.cs](../../AgentSession.MCP.Tests/McpSessionTests.cs) | protocol versions, discovery, descriptions, schemas and real calls |

## Operations files

| File | Purpose |
| --- | --- |
| [docker-compose.ai-learning.yml](../../docker-compose.ai-learning.yml) | Loopback Qdrant with central bind-mounted persistence |
| [Start-AgentMemoryQdrant.ps1](../../scripts/Start-AgentMemoryQdrant.ps1) | Render/start Qdrant without installing dependencies |
| [Stop-AgentMemoryQdrant.ps1](../../scripts/Stop-AgentMemoryQdrant.ps1) | Stop without deleting persistent data |
| [Show-AgentMemoryQdrantConfig.ps1](../../scripts/Show-AgentMemoryQdrantConfig.ps1) | Display resolved Compose configuration |
| [Test-AgentMemoryDependencies.ps1](../../scripts/Test-AgentMemoryDependencies.ps1) | Validate root, Qdrant REST/gRPC, Ollama and model |
| [Backup-AgentMemory.ps1](../../scripts/Backup-AgentMemory.ps1) | Create bounded backup of central state |
| [Restore-AgentMemory.ps1](../../scripts/Restore-AgentMemory.ps1) | Restore with explicit safety checks |
| [Reset-AgentMemoryDerivedIndexes.ps1](../../scripts/Reset-AgentMemoryDerivedIndexes.ps1) | Remove only scoped rebuildable indexes |
| [Test-PublishedMcp.ps1](../../scripts/Test-PublishedMcp.ps1) | Launch published executable and verify clean discovery |

## Planning and contract sources

- [dotnet-mcp-agent-memory-implementation-prompt.md](../../Requirements/dotnet-mcp-agent-memory-implementation-prompt.md) is the original production requirement source.
- [proposal.md](../../openspec/changes/archive/2026-09-27-productionize-agent-memory/proposal.md) records change purpose and scope.
- [design.md](../../openspec/changes/archive/2026-09-27-productionize-agent-memory/design.md) records architecture decisions and scenarios.
- [specs](../../openspec/changes/archive/2026-09-27-productionize-agent-memory/specs/) contain normative delta requirements.
- [tasks.md](../../openspec/changes/archive/2026-09-27-productionize-agent-memory/tasks.md) maps implementation and verification work.
- [verification.md](../../openspec/changes/archive/2026-09-27-productionize-agent-memory/verification.md) records achieved evidence and remaining release boundary.

## Documentation sources

`AiLearning` documents the runtime contract and operations. `wiki/agent-ai-guidence` teaches consumers how to design agents and skills. This `wiki/Dev` set teaches maintainers how the implementation works.
