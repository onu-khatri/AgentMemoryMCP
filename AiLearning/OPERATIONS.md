# Local operations

Run commands from the repository root. Replace `agent-memory-mcp` with the repository identity chosen for this checkout. Keep that identity stable when moving the checkout; use a different ID for an independent repository. Worktrees intentionally share data only when they use the same ID.

## Prerequisites and health

The scripts report missing prerequisites and never install Docker, Qdrant, Ollama or models.

```powershell
$root = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex/AgentMemory'
ollama pull embeddinggemma
./scripts/Show-AgentMemoryQdrantConfig.ps1 -SystemRoot $root
./scripts/Start-AgentMemoryQdrant.ps1 -SystemRoot $root
./scripts/Test-AgentMemoryDependencies.ps1 -SystemRoot $root -EmbeddingModel embeddinggemma
```

The dependency report must show writable vector storage, healthy Qdrant REST and gRPC, healthy Ollama, and the requested model. Stop the Compose project without deleting persisted data:

```powershell
./scripts/Stop-AgentMemoryQdrant.ps1 -SystemRoot $root
```

Qdrant binds only to loopback by default. Its bind-mounted data is `<system-root>/.vector/qdrant`. Do not use `docker compose down --volumes`, delete `.vector`, or remove shared collections as a repository reset.

## Server and client

```powershell
dotnet restore AgentMemoryMCP.slnx
dotnet build AgentMemoryMCP.slnx -c Release --no-restore
$env:Repository__Id = 'agent-memory-mcp'
$env:SystemStorage__Root = $root
dotnet run --project AgentSession.MCP/AgentSession.MCP.csproj -c Release --no-build
```

Optional local overrides use normal .NET environment names:

```powershell
$env:Embedding__Ollama__BaseUrl = 'http://127.0.0.1:11434'
$env:Embedding__Ollama__Model = 'embeddinggemma'
$env:Qdrant__Host = '127.0.0.1'
$env:Qdrant__GrpcPort = '6334'
$env:Qdrant__RestPort = '6333'
$env:Qdrant__Collection = 'repo_ai_learning_v1'
```

Only loopback Ollama and Qdrant endpoints are accepted. Inspect health with `memory_status`; run `memory_cleanup` for a bounded expiry/index/archive-candidate maintenance pass. Use `memory_rebuild_indexes` for filesystem indexes and `memory_reindex` for `file_system`, `pending_embeddings`, `specific_memory_ids`, `qdrant`, `model_migration` or `all`.

## Backup and restore

Stop MCP processes and the Qdrant Compose project before a coherent full-root backup. The backup includes sessions, canonical learning, journals, grants and vector data.

```powershell
./scripts/Stop-AgentMemoryQdrant.ps1 -SystemRoot $root
./scripts/Backup-AgentMemory.ps1 -SystemRoot $root -Destination 'D:/Backups/AgentMemory-2026-09-27.zip'
```

Restore into a new, absent directory, inspect it, then point the server and Qdrant scripts at that root. The restore refuses an existing target and rejects archive entries that escape staging.

```powershell
$restored = 'D:/Restores/AgentMemory-2026-09-27'
./scripts/Restore-AgentMemory.ps1 -BackupPath 'D:/Backups/AgentMemory-2026-09-27.zip' -SystemRoot $restored -ConfirmRestore
./scripts/Start-AgentMemoryQdrant.ps1 -SystemRoot $restored
./scripts/Test-AgentMemoryDependencies.ps1 -SystemRoot $restored
```

## Model migration and rollback

Changing the configured model or its Ollama digest makes the active repository collection incompatible. Pull/select the intended model and run `memory_reindex` with scope `model_migration`. The server builds and verifies a versioned repository collection, replays concurrent canonical changes, and switches the alias only after completeness checks. Previous collections are retained.

To roll back, restore the prior `Embedding__Ollama__Model` configuration and available model digest, restart the MCP server, then run `memory_reindex` with scope `model_migration`. Do not hand-edit aliases or delete shared collections. If the prior model is unavailable, keep canonical files intact and report semantic recall as blocked until that model is restored or a forward migration succeeds.

## Archive recovery

Use `memory_get` with `includeArchived: true` for an explicit archived short-term ID. Archives are bounded `.jsonl.gz` files with ID/hash manifests. Ordinary recall does not scan them.

If an archive or manifest is corrupt, stop mutations, preserve the files, and restore a known-good full-root backup into a new root. Never delete active canonical records to make a broken archive appear consistent. Incomplete managed operations are recovered on startup; `memory_status` reports the recovery backlog and archive timestamps.

## Repository-scoped derived-index reset

Stop MCP processes for the target repository, review the identity, then remove only its disposable filesystem index metadata:

```powershell
./scripts/Reset-AgentMemoryDerivedIndexes.ps1 -SystemRoot $root -RepositoryId 'agent-memory-mcp' -ConfirmReset
```

The script leaves canonical temp/short/long records, archives, sessions, other repositories and all Qdrant collections intact. Restart the server, run `memory_rebuild_indexes`, then use `memory_reindex` with `specific_memory_ids`, `qdrant` or `all` as needed. Qdrant repair remains repository-scoped through the MCP reindex/migration path.

