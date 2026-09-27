# Operations and semantic dependencies

[Wiki index](README.md)

Session coordination and canonical filesystem memory do not depend on Ollama or Qdrant. Semantic long-term recall does. Agents should degrade honestly and leave operational changes to explicit maintenance workflows.

## Health

Call `memory_status` to inspect:

- repository and storage roots;
- tier and lifecycle counts;
- expired and review-due records;
- recovery and pending-embedding backlog;
- Ollama model health;
- Qdrant collection health;
- index health and last maintenance activity.

Status returns no memory content and is not a substitute for resume or recall.

## Local dependency commands

```powershell
$root = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex/AgentMemory'
ollama pull embeddinggemma
./scripts/Start-AgentMemoryQdrant.ps1 -SystemRoot $root
./scripts/Test-AgentMemoryDependencies.ps1 -SystemRoot $root -EmbeddingModel embeddinggemma
```

Qdrant binds to loopback and persists below `<system-root>/.vector/qdrant`. Stopping the container must preserve that bind-mounted data. Canonical learning remains sufficient to rebuild vectors if the container data is lost.

## Maintenance tool selection

- `memory_cleanup`: expire temp records and repair filesystem catalogs within bounds. It makes no semantic decisions.
- `memory_rebuild_indexes`: rebuild derived filesystem indexes only.
- `memory_reindex`: process selected IDs, pending embeddings, Qdrant rebuild, all scopes or controlled embedding-model migration.

Example selected-ID reindex:

```json
{
  "operationId": "reindex-payments-records",
  "scope": "specific_memory_ids",
  "memoryIds": ["memory-a", "memory-b"],
  "maxItems": 20
}
```

Model migration builds and verifies a compatible versioned collection before switching the active alias. Previous collections remain available for rollback. Do not delete shared collections or `.vector` as a routine repository reset.

## Outage behavior for agents

When semantic dependencies are unavailable:

1. Continue session resume, artifacts, coordination and checkpoints.
2. Continue temp and short-term canonical writes.
3. Allow long-candidate creation; pending work remains durable.
4. Use labeled lexical recall and include health reasons in conclusions.
5. Do not claim semantic completeness or fabricate similarity scores.
6. Let a bounded maintenance operation reconcile pending vectors after recovery.

## Backup principle

Back up the central canonical storage and configuration. Vector data may be backed up for faster recovery, but it is not the only copy of learning. Follow `AiLearning/OPERATIONS.md` for backup, restore, archive recovery, model rollback and scoped reset commands.

## Real-life example: Qdrant container is deleted

Docker's running Qdrant container and its disposable collection are removed, but the central canonical records remain. Agents continue to resume sessions and use lexical recall. An operator restores Qdrant and runs a controlled `memory_reindex` Qdrant or model-migration scope. The server rebuilds points from canonical long-term records, verifies completeness and switches the alias only after success. No agent recreates learning from chat history.

