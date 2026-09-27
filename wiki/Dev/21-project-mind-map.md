# Project mind map

[Previous: Build and demo](20-build-run-and-demo.md) | [Developer wiki](README.md) | [Next: Glossary](22-glossary.md)

```mermaid
mindmap
  root((AgentMemoryMCP))
    Purpose
      Restart continuity
      Multi-agent coordination
      Token-efficient local memory
      Reusable repository learning
    MCP surface
      Session tools
        Discover and activate
        Append structured artifacts
        Coordinate tasks
        Resume and checkpoint
      Memory tools
        Remember, get, update, recall
        Review and compaction
        Archive and delete
        Outcomes and lifecycle
        Status and reindex
    Session domain
      Context
      Plans and decisions
      Findings and outputs
      Handoffs
      Tasks
        Work keys
        Dependencies
        Leases
        Claim tokens
        Evidence
      Snapshots
        Immutable pages
        Per-agent checkpoints
    Learning domain
      Temp
        Maximum 24 hours
        No embeddings
      Short
        Reviewable
        Compact/archive/promote
      Long
        Candidate
        Validated
        Superseded
        Retired
      Provenance
      Outcomes
      Relationships
    Durability
      Managed paths
      Repository lock
      Atomic file replace
      Durable intent
      Roll-forward recovery
      Operation receipt
      Optimistic revision
      Rebuildable catalog
    Semantic retrieval
      Projection v1
      Ollama embeddinggemma
      Fingerprint
      Qdrant
        Versioned collection
        Stable alias
        Payload filters
        Deterministic point IDs
      Pending work
      Reconciliation
      Migration and rollback
      Lexical degradation
    Security
      Repository isolation
      Path traversal defense
      Secret content policy
      Loopback dependencies
      External operator grants
      Advisory memory
      Opaque evidence
    Operations
      Hosted maintenance
      Backup and restore
      Scoped reset
      Dependency checks
      Published smoke
    Verification
      Unit and filesystem
      MCP subprocess
      Multi-process recovery
      Live Ollama/Qdrant
      Windows and Linux publish
```

## How to use the map

Start at a requested behavior and follow downward to its domain, durability and verification branches. For example, “semantic recall returned a stale item” crosses learning lifecycle, Qdrant payload, canonical hit verification, reconciliation and the live vector test suite.

