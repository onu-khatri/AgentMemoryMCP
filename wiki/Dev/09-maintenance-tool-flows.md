# Maintenance tool end-to-end flows

[Previous: Lifecycle tools](08-learning-lifecycle-tool-flows.md) | [Developer wiki](README.md) | [Next: Semantic pipeline](10-semantic-pipeline.md)

## `memory_cleanup`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.RunMaintenanceAsync]
    B --> C[CleanupAsync]
    C --> D[Find bounded expired temp entries]
    D --> E[Revalidate canonical expiry]
    E --> F[Commit file + catalog removal]
    F --> G[List active short records]
    G --> H{Explicit archive_candidate?}
    H -->|yes| I[Call ArchiveAsync with deterministic operation ID]
    H -->|no| J[Retain record]
    I --> K[Aggregate deleted, archived and errors]
    J --> K
    K --> L[CleanupMemoryResult]
```

Age creates suggestions elsewhere; only an explicit archive-candidate review state is processed automatically.

## `memory_rebuild_indexes`

```mermaid
flowchart TD
    A[Tool call] --> B[LearningCatalog.RebuildAsync]
    B --> C[Scan bounded canonical directories]
    C --> D[Deserialize and validate every record]
    D --> E{Identity, path and hash valid?}
    E -->|yes| F[Create catalog entry and shard]
    E -->|no| G[Report invalid ID; leave file untouched]
    F --> H[Increment manifest generation]
    G --> H
    H --> I[Commit manifest + shards]
    I --> J[Return invalid-record count]
```

This tool does not touch embeddings or Qdrant.

## `memory_reindex`

```mermaid
flowchart TD
    A[Tool call] --> B[MemoryReindexService.ReindexAsync]
    B --> C[Validate scope, IDs, operation ID and maxItems]
    C --> D{Scope}
    D -->|file_system| E[LearningCatalog.RebuildAsync]
    D -->|qdrant/model_migration| F[VectorMigrationService.MigrateAsync]
    D -->|specific_memory_ids| G[Reconcile selected records against active collection]
    D -->|pending_embeddings| H[VectorIndexCoordinator.ProcessPendingAsync]
    D -->|all| I[Rebuild + migrate + process pending]
    G --> H
    E --> J[Aggregate metrics]
    F --> J
    H --> J
    I --> J
    J --> K[ReindexMemoryResult]
```

`MemoryReindexScope` values are file system, Qdrant, pending embeddings, specific memory IDs, model migration and all. Model migration retains the previous collection.

## Maintenance ownership

Manual tools are bounded and cancellable. `MemoryMaintenanceService` uses the same underlying services on a periodic ten-second budget; it never invents semantic content or validates candidates.

