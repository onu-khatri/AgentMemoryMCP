# Curation tool end-to-end flows

[Previous: Core memory](06-core-memory-tool-flows.md) | [Developer wiki](README.md) | [Next: Lifecycle tools](08-learning-lifecycle-tool-flows.md)

## `memory_list_review`

```mermaid
flowchart LR
    A[Tool call] --> B[CanonicalMemoryService.ListReviewAsync]
    B --> C[Validate result bound and filters]
    C --> D[Read catalog and canonical short records]
    D --> E[Apply state, age, category, decision area, agent and task filters]
    E --> F[Calculate suggestion from policy dates]
    F --> G[Bounded ReviewMemoryCandidate list]
```

Suggestions are not approval.

## `memory_mark_reviewed`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.ReviewAsync]
    B --> C[Validate one of five allowed review actions]
    C --> D[Replay check + load short record by revision]
    D --> E[Set review state, reason, evidence, actor and timestamp]
    E --> F[For keep: schedule next review]
    F --> G[Commit record + catalog + event + receipt]
    G --> H[MemoryWriteResult]
```

The five actions are keep, compact candidate, promotion candidate, archive candidate and delete candidate. No lifecycle action is performed here.

## `memory_prepare_compaction`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.PrepareCompactionAsync]
    B --> C[Validate at least two bounded sources]
    C --> D[Load every source at expected revision]
    D --> E{All short, reviewed and eligible?}
    E -->|no| F[review_required or validation error]
    E -->|yes| G[Hash exact source identity/revisions/content]
    G --> H[Persist/replay preparation receipt]
    H --> I[PreparedCompactionResult with unchanged sources + token]
```

## `memory_commit_compaction`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.CommitCompactionAsync]
    B --> C[Validate preparation token and full request]
    C --> D[Reload exact source revisions]
    D --> E[Check every source has sourceCoverage]
    E --> F[Check evidence and unresolved contradictions preserved]
    F --> G[Build compact short record with relationships]
    G --> H[Create bounded gzip JSONL + manifest]
    H --> I[Read back and verify archive hashes/count/limits]
    I --> J[Commit compact record + archive + source removal + catalog + receipt]
    J --> K[CompactionResult]
```

The server validates structural coverage, not semantic truth.

## `memory_compact`

```mermaid
flowchart LR
    A[Tool call with complete summary] --> B[CanonicalMemoryService.CompactAsync]
    B --> C[Internally prepare exact sources]
    C --> D[Build commit request with returned token]
    D --> E[CommitCompactionAsync]
    E --> F[CompactionResult]
```

This convenience path is appropriate only when the caller already reviewed every source.

## `memory_archive`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.ArchiveAsync]
    B --> C[Validate selected short records and revisions]
    C --> D{Protected?}
    D -->|yes| E[OperatorGrantVerifier.Require archive grant]
    D -->|no| F[Build archive entries]
    E --> F
    F --> G[Compress JSONL within bounds]
    G --> H[Decompress and verify IDs, hashes and manifest]
    H --> I[Commit archive + manifest + remove active files + update catalog]
    I --> J[ArchiveMemoryResult]
```

Source removal never precedes verified archive construction.

## `memory_delete`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.DeleteAsync]
    B --> C[Validate actor, reason, revision and replay]
    C --> D[Load canonical record]
    D --> E{Tier temp or short?}
    E -->|no| F[Reject: retire long-term]
    E -->|yes| G{Protected?}
    G -->|yes| H[Require delete grant]
    G -->|no| I[Prepare deletion]
    H --> I
    I --> J[Commit file removal + catalog removal + sanitized event + receipt]
    J --> K[MemoryWriteResult]
```

Deletion events omit raw deleted content.

## Compaction data model

`CompactionDetails` stores source IDs, summary, facts, questions, contradictions, patterns, evidence and per-source coverage. Relationships connect compacted output back to sources. Archives remain explicitly readable with `memory_get(includeArchived=true)`.

