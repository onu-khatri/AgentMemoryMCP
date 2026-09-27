# Core memory tool end-to-end flows

[Previous: Session tools](05-session-tool-flows.md) | [Developer wiki](README.md) | [Next: Curation tools](07-curation-tool-flows.md)

These tools enter through `MemoryTools`. Canonical behavior is owned by `CanonicalMemoryService`; search also uses `OllamaEmbeddingClient`, `VectorMigrationService` and `QdrantVectorIndex`.

## `memory_status`

```mermaid
flowchart LR
    A[Tool call] --> B[MemoryStatusService.GetAsync]
    B --> C[Canonical status snapshot]
    B --> D[Transaction recovery backlog]
    B --> E[MaintenanceState snapshot]
    B --> F[Ollama model discovery]
    B --> G[Active vector identity + Qdrant readiness]
    C --> H[Combine counts and health]
    D --> H
    E --> H
    F --> H
    G --> H
    H --> I[MemoryStatusResult without content]
```

Dependency failures become structured degraded states rather than destroying canonical data.

## `memory_remember`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.RememberAsync]
    B --> C[Validate request, IDs, bounds and secret policy]
    C --> D[Check operation receipt]
    D -->|replay| E[Return prior result]
    D -->|new| F[Cleanup expired temp + read catalog]
    F --> G[Map temp/short/long_candidate to tier and state]
    G --> H[Calculate expiry, content hash and dedupe scope]
    H --> I{Active duplicate?}
    I -->|yes| J[Commit dedupe receipt/event]
    I -->|no| K[Check tier capacity]
    K --> L[Create canonical record]
    L --> M[For long: mark embedding/index pending]
    M --> N[Commit record + catalog + event + receipt]
    J --> O[MemoryWriteResult duplicate=true]
    N --> P[MemoryWriteResult]
```

Deduplication uses normalized content plus structured data and a scope containing repository, tier, category, decision area and temp session.

## `memory_get`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.GetAsync]
    B --> C[Validate memory ID]
    C --> D[Cleanup expired temp + read catalog]
    D --> E{Catalog entry exists?}
    E -->|no + includeArchived| F[Bounded verified archive lookup]
    E -->|no| G[not_found]
    E -->|yes| H{Expired?}
    H -->|yes| I[expired]
    H -->|no| J{Historical and includeHistory=false?}
    J -->|yes| K[historical]
    J -->|no| L[Read and fully validate canonical record]
    L --> M[MemoryReadResult advisory=true]
```

Canonical validation compares identity, path, revision, state, timestamps and hashes with the catalog entry.

## `memory_update`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.UpdateAsync]
    B --> C[Validate content, operation, actor and ID]
    C --> D[Replay check + catalog read]
    D --> E[Load canonical record at expected revision]
    E --> F{Tier is temp?}
    F -->|no| G[Reject: durable learning uses curation]
    F -->|yes| H{Expiry extends lifetime?}
    H -->|yes| I[Reject]
    H -->|no| J[Replace fields, increment revision, recalculate hash]
    J --> K[Commit record + catalog + event + receipt]
    K --> L[MemoryWriteResult]
```

Creation time is preserved. Temp expiry can only stay the same or shorten.

## `memory_recall`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.RecallAsync]
    B --> C[Validate filters, tiers, limits and time range]
    C --> D[Cleanup + read canonical catalog]
    D --> E[Select eligible temp and short records]
    E --> F{Empty query?}
    F -->|yes| G[Recent filtered canonical results]
    F -->|no| H[Lexical scoring for temp/short and fallback long]
    H --> I{Long requested and semantic dependencies compatible?}
    I -->|yes| J[Embed query with active fingerprint]
    J --> K[Qdrant search with repository and metadata filters]
    K --> L[Verify every hit against current canonical revision/hash/state]
    I -->|no| M[Add health reason and eligible lexical long results]
    G --> N[Tier order + overall bound]
    L --> N
    M --> N
    N --> O[RecallMemoryResult with mode, matchType and score]
```

Qdrant hits never bypass canonical verification. Retired, superseded, contradicted and candidate records require explicit historical flags.

