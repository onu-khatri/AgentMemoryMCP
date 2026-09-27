# Learning lifecycle tool end-to-end flows

[Previous: Curation](07-curation-tool-flows.md) | [Developer wiki](README.md) | [Next: Maintenance tools](09-maintenance-tool-flows.md)

## `memory_record_event`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.RecordObservationEventAsync]
    B --> C[Validate IDs, metadata, content policy]
    C --> D{eventType starts observation-*?}
    D -->|no| E[event_type_reserved]
    D -->|yes| F[Check event receipt]
    F -->|replay| G[Return original event]
    F -->|new| H[Create metadata-only MemoryEvent]
    H --> I[Append to bounded rotating journal + receipt transaction]
    I --> J[MemoryEvent]
```

Caller events cannot impersonate authoritative lifecycle events emitted by services.

## `memory_record_outcome`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.RecordOutcomeAsync]
    B --> C[Validate durable target, evidence and outcome ID]
    C --> D[Replay check + expected revision]
    D --> E[Create immutable outcome record]
    E --> F[Increment use and result counters]
    F --> G{Independent success?}
    G -->|yes| H[Add confirmation]
    G -->|no| I[Keep existing confirmations]
    H --> J[Recalculate confidence]
    I --> J
    J --> K[Mark stale/contradicted flags when applicable]
    K --> L[Commit canonical revision + outcome + event + receipt]
    L --> M[MemoryWriteResult]
```

Confidence is `(1 + successes + 0.5 * partialSuccesses) / (2 + successes + partialSuccesses + failures + contradictions)`.

## `memory_promote`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.PromoteAsync]
    B --> C[Load record at expected revision]
    C --> D{Current tier/state}
    D -->|temp| E[Create short record, preserve provenance]
    D -->|short| F[Create long candidate, mark vector pending]
    D -->|long candidate| G[Evaluate confirmations or authoritative evidence]
    G --> H{High risk / grant required?}
    H -->|yes| I[OperatorGrantVerifier.Require validation grant]
    H -->|no| J[Validate candidate]
    I --> J
    E --> K[Commit move + catalog + events + receipt]
    F --> K
    J --> K
    K --> L[MemoryWriteResult]
```

Promotion never makes advisory memory override current evidence.

## `memory_supersede`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.SupersedeAsync]
    B --> C[Validate actor, reason, evidence and revisions]
    C --> D[Load active long source and canonical replacement]
    D --> E[Add Supersedes/SupersededBy relationships]
    E --> F[Move source to superseded state/path]
    F --> G[Update replacement history and vector pending state]
    G --> H[Commit both records + catalog + event + receipt]
    H --> I[MemoryWriteResult]
```

## `memory_retire`

```mermaid
flowchart TD
    A[Tool call] --> B[CanonicalMemoryService.RetireAsync]
    B --> C[Validate active long record, revision, reason and evidence]
    C --> D[Optionally validate replacement link]
    D --> E[Set retired state and historical relationships]
    E --> F[Move canonical file to retired path]
    F --> G[Update catalog and vector pending/removal state]
    G --> H[Commit event + receipt]
    H --> I[MemoryWriteResult]
```

Superseded and retired records disappear from ordinary recall immediately through canonical eligibility checks, even if a stale Qdrant point temporarily remains.

## Lifecycle states

Long-term states include candidate, validated, superseded, retired and rejected. Candidate creation and validation are independent from embedding/index state: semantic outages cannot silently change knowledge status.

