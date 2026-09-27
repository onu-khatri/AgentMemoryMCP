## Purpose

Recall relevant repository learning using local embeddings and a disposable vector index with safe outage and migration behavior.

## ADDED Requirements

### Requirement: Local model-compatible embeddings
Long-term candidates and validated records SHALL support local Ollama embeddings using embeddinggemma by default and no cloud credentials. Temporary and short-term records SHALL NOT be embedded automatically. The server SHALL validate model identity/version, actual dimensions, finite vectors and batch correspondence. It SHALL reject silent truncation and incompatible collection use, including same-dimensional model changes.

#### Scenario: Model changes without rebuild
- **WHEN** the configured model fingerprint differs from the active collection
- **THEN** incompatible queries/writes are blocked or routed through the still-active matching model and migration is reported as required

#### Scenario: Oversized embedding input
- **WHEN** an input exceeds the model context
- **THEN** canonical content is retained and an explicit embedding error is reported without a truncated or fake vector

### Requirement: Repository-filtered vector index
Qdrant SHALL index long-term records with repository ID, lifecycle, content hash, embedding metadata, category, decision area, tags, confidence, counters, timestamps and source type. Semantic queries SHALL constrain repository and requested metadata. Canonical files SHALL remain sufficient to rebuild all points.

#### Scenario: Cross-repository similar text
- **WHEN** another repository has a higher-scoring point
- **THEN** it is excluded from recall for the current repository

### Requirement: Unified bounded recall
Recall SHALL support query, tiers, repository/session/task, decision areas, categories, tags, lifecycle, maxResults, minimumSimilarity, includeArchived and includeRetired. Defaults SHALL order eligible temp, active/compacted short, then validated long memory with ten results maximum by default. Empty query SHALL return recent filtered records. Candidates, stale, contradicted, superseded, retired and archived records SHALL be excluded from ordinary recall. Explicit historical filters SHALL permit appropriate diagnostic lookup. Returned results SHALL distinguish lexical scores from semantic similarity and carry provenance.

#### Scenario: Default mixed-tier query
- **WHEN** matching eligible records exist across tiers
- **THEN** results obey tier order, filters, canonical status and the overall bound

### Requirement: Degraded local operation
Ollama or Qdrant failures SHALL preserve canonical data and permit temp/short operations, lexical recall and long candidate creation. Responses/status SHALL disclose degraded dependency health and durable pending work. Recovery SHALL retry idempotently with bounded backoff and cancellation without fake embeddings.

#### Scenario: Dependencies offline and restored
- **WHEN** candidates are created during an outage and dependencies later recover
- **THEN** local operations continue and pending candidates become indexed exactly once per current revision

### Requirement: Rebuild and atomic model migration
Reindex SHALL support filesystem indexes, Qdrant, pending work, selected IDs, model migration and all, scoped to the bound repository. Model migration SHALL build a compatible versioned collection, reconcile concurrent canonical changes, validate completeness and switch active collection only after success. Previous collections SHALL remain available for explicit rollback. Shared indexes or other repositories SHALL NOT be deleted by one repository's rebuild.

#### Scenario: Failed rebuild
- **WHEN** embedding or verification fails before switching collections
- **THEN** the prior compatible collection remains active and canonical records are unchanged

#### Scenario: Full reconstruction
- **WHEN** an isolated test vector collection is removed and reindex runs
- **THEN** canonical records reconstruct semantic retrieval, including a paraphrased query, without original vectors
