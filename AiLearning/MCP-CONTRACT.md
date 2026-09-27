# MCP contract

The six session tools and 20 memory tools are registered once through the stdio MCP server. Session operations and canonical filesystem memory work without Ollama or Qdrant. JSON uses camelCase fields and snake_case enums. Each tools/call supplies `arguments: { "request": { ... } }` unless the tool has no request object. Unknown fields, numeric enums and obsolete freeform payloads are rejected.

## Agent memory operating model

Treat this server as the primary persisted memory source for repository work. Keep the live context small: retrieve the bounded state needed for the current step instead of carrying the full conversation or rediscovering completed work. Persist conclusions, evidence, ownership, outputs and next actions often enough that another agent can continue after a restart.

At the start of work:

1. If the session ID is unknown, page through `list_agent_sessions` and select the existing session that matches the work. Do not create a duplicate session for known work.
2. Call `create_or_activate_session` with the chosen stable ID. Activation establishes identity but does not load content.
3. Call `resume_agent_session`, consume every page of its consistent snapshot and use the returned context, tasks, artifacts, changes, blockers and handoffs before planning or repeating work.
4. Call `memory_recall` with a focused query, filters and a small result bound when reusable knowledge from this or another session may apply. Call `memory_get` only when the exact record ID is known or a full recalled record is needed.
5. Verify recalled advice against current code, tests, repository instructions and user decisions. Persisted memory is the primary continuity mechanism, while those current sources remain authoritative.

While working, choose the write path by meaning:

| Need | Tool | Boundary |
| --- | --- | --- |
| Save the current objective, plan, decision, finding, output or handoff for agents sharing this session | `append_agent_memory` | Session-specific operational state; not task ownership and not general reusable learning. |
| Create, claim, renew, block, complete or reopen delegated work | `coordinate_agent_task` | The only authoritative task-state mutation path. |
| Save a reusable fact, pattern or evidence that should survive beyond one session | `memory_remember` | Concise advisory learning; not transcripts, full session state, secrets or hidden reasoning. |
| Report whether a durable memory helped or failed | `memory_record_outcome` | Reliability feedback tied to real evidence; not an activity log or task completion. |
| Record metadata about an external observation | `memory_record_event` | `observation-*` metadata only; not content, outcomes or lifecycle transitions. |
| Acknowledge that a resume snapshot was fully consumed | `checkpoint_agent_session` | Advances this agent's read position; saves no context or handoff. |

Before stopping or handing off, update the structured shared context, save relevant findings and outputs, complete or release owned tasks with evidence, and add a handoff with concrete next actions. Then resume once more if concurrent work may have arrived. Checkpoint only after the full snapshot has been consumed. This sequence makes persisted tools useful as local agent memory without using the prompt as a growing transcript store.

## Client configuration

Build before connecting so stdout remains protocol-only. The process binds one stable repository identity and writes runtime data only below the central root.

```json
{
  "mcpServers": {
    "agent-memory": {
      "command": "dotnet",
      "args": ["run", "--no-build", "--project", "D:/RND/McpServers/AgentMemoryMCP/AgentSession.MCP/AgentSession.MCP.csproj"],
      "env": {
        "Repository__Id": "agent-memory-mcp",
        "SystemStorage__Root": "C:/Users/example/.codex/AgentMemory"
      }
    }
  }
}
```

## Shared context and contributions

Call create_or_activate_session with sessionId, actorId and operationId. Subsequent calls name the session explicitly; activation does not set process-wide state. IDs are lowercase filesystem-safe identifiers, never paths.

Example append_agent_memory request:

```json
{
  "sessionId": "parser-work",
  "actorId": "planner",
  "operationId": "save-context-1",
  "updates": [{
    "expectedRevision": 0,
    "artifact": {
      "id": "context",
      "kind": "context",
      "actorId": "planner",
      "title": "Shared context",
      "context": {
        "objective": "Implement and verify the parser",
        "planReferences": ["parser-plan"],
        "constraints": [],
        "nextActions": ["Claim parser-tests"]
      }
    }
  }]
}
```

Only one shared context slot exists: ID context. Null fields mean unknown; empty lists mean explicitly recorded empty lists. Updates replace the full context and require its current revision. Conflicts require explicit reconciliation, never per-agent precedence.

Other artifact kinds are decision, finding, handoff, plan and output. They use object data instead of context, and support evidence references and optional taskId. Plans can include acceptance criteria, steps, constraints and validation in data. Context planReferences selects current plans; timestamps and caller approval claims are not authorization. Artifact kind and task association cannot change after creation. Task-linked updates require claimToken beside artifact and expectedRevision.

The atomic batch returns sequence and artifactRevisions. Expected revision zero means create. Retry identical mutations with the same operation ID after a lost response; reusing an ID with different content fails. Mutation IDs are scoped by repository/session. Reread and explicitly reconcile revision conflicts with a new ID. Task state can change only through coordinate_agent_task, never artifact data.

## Tasks and parent/sub-agent workflow

A parent creates tasks using stable workKey, taskId, title, optional parentStepId, dependencies, acceptanceExpectations and nextAction. References must exist in the same session. Repeated identical work definitions resolve to one task; different definitions conflict. The child resumes shared context, claims its task, contributes linked findings with its token and records outputs/evidence/handoff on completion. The parent resumes and reuses those outputs without repeating the work.

Claims return ownerId, claimToken, fencingGeneration and claimExpiresAtUtc. Default lease is 15 minutes (`SessionCoordination__ClaimLeaseMinutes`). Claims require complete prerequisites; renew/release/block/complete require the current revision and unexpired token. Explicit takeover after expiry fences the prior token, including after restart. Claims cannot prevent external filesystem or tool side effects; check ownership before such effects and reconcile work on takeover.

Block requires reason, retains evidence and releases ownership. Complete requires outputs or handoff; verificationStatus is unverified, passed, failed or partial. Verification claims require evidence; the server does not execute the verification. Completed/blocked tasks require audited reopen with a reason before claiming again.

## Resume and checkpoint

resume_agent_session takes sessionId, actorId, optional pageSize (1–100 by default) and cursor. It returns snapshotId, sequence, acknowledgedSequence, entries, unknown, hasMore and cursor. Artifact/task entries reflect a consistent snapshot; change entries cover commits since this agent's checkpoint. Read every page. Later contributions appear in a new snapshot.

Snapshots default to 60 minutes (`SessionCoordination__SnapshotLifetimeMinutes`), persist through restart and use immutable hash-checked chunks. Pages are bounded by count and bytes and may be smaller than requested. Invalid, missing or expired cursors require a fresh call without a cursor. Reads never acknowledge progress.

checkpoint_agent_session requires sessionId, actorId, snapshotId and its exact sequence. It rejects partially delivered snapshots, another actor's snapshot, expiry, future sequences and backwards movement. Save a handoff with append, then obtain a new snapshot before acknowledging that contribution.

## Memory tools

The memory surface contains exactly these tools:

| Tool | Purpose |
| --- | --- |
| `memory_status` | Counts, roots, recovery backlog, dependencies, model/collection health and last maintenance activity. |
| `memory_remember` | Create canonical `temp`, `short` or `long_candidate` memory with a replay operation ID. |
| `memory_get` | Read a canonical record; archived/history reads require explicit flags. |
| `memory_update` | Revision-controlled temporary-memory replacement without extending its lifetime. |
| `memory_recall` | Bounded tier-ordered recent, lexical or semantic advisory recall with metadata filters. |
| `memory_list_review` | List bounded short-term review candidates; age is a suggestion only. |
| `memory_mark_reviewed` | Record an intentional review action, reason and evidence. |
| `memory_prepare_compaction` | Return unchanged reviewed sources and a cryptographic preparation token. |
| `memory_commit_compaction` | Commit an agent-written, fully covered representation and verified archive. |
| `memory_compact` | Perform the same prepare/commit policy in one request. |
| `memory_delete` | Explicitly remove temp/short memory; protected records require an operator grant. |
| `memory_archive` | Move selected short records to a verified bounded JSONL+gzip archive. |
| `memory_record_event` | Add a replay-safe metadata-only `observation-*` event. |
| `memory_record_outcome` | Record one replay-safe outcome and update advisory reliability metadata. |
| `memory_promote` | Promote temp to short, short to long candidate, or validate an eligible candidate. |
| `memory_supersede` | Replace long-term learning while preserving bidirectional history. |
| `memory_retire` | Exclude long-term learning from ordinary recall without physical deletion. |
| `memory_reindex` | Run bounded filesystem, vector, pending, selected-ID or model-migration work. |
| `memory_cleanup` | Run deterministic expiry, index and approved archive-candidate maintenance. |
| `memory_rebuild_indexes` | Rebuild only filesystem indexes from canonical records. |

`memory_remember` never creates validated long-term learning. A candidate needs independent successful outcomes and an explicit promotion; high-risk validation also needs an operator-managed grant scoped to repository, record revision and action. MCP tools cannot create grants.

Example semantic recall:

```json
{
  "request": {
    "query": "How do agents avoid repeating completed parser work?",
    "tiers": ["short", "long"],
    "decisionAreas": ["parser"],
    "tags": ["verified"],
    "maxResults": 10,
    "minimumSimilarity": 0.55
  }
}
```

Semantic results are labeled with their match type and score. If Ollama or Qdrant is unavailable, eligible canonical data remains intact and recall returns labeled lexical results with structured degradation reasons. No fake semantic score is produced.

Example compaction preparation:

```json
{
  "request": {
    "operationId": "prepare-parser-compaction-1",
    "agentId": "curator",
    "sources": [
      { "memoryId": "mem-a", "expectedRevision": 2 },
      { "memoryId": "mem-b", "expectedRevision": 1 }
    ]
  }
}
```

The commit repeats those sources and includes the returned preparation token, summary, facts, open questions, contradictions, successful/failed patterns, evidence, tags and one `sourceCoverage` item for every source. Each coverage item must preserve that source's evidence and unresolved contradictions. Sources must already be reviewed and unchanged. Originals are archived by default; disabling archival is denied in this release.

Memory is advisory. It does not override current repository content, tests, instructions, approvals or user decisions. Contracts intentionally provide no field for private chain-of-thought; save concise conclusions, evidence and operational rationale.

## Bounds, errors and trust

Defaults: 64 KiB contribution/task payload, 1 MiB request/record, depth 32, 100 logical records/batch and 100 entries/page. Internal metadata, index, journal and replay files have a separate bounded transaction allowance. Limits fail explicitly without silent eviction or partial commits. Session metadata and snapshot manifests are subject to record limits.

MCP tool errors use stable machine-readable prefixes:

| Code | Meaning and caller action |
| --- | --- |
| `invalid_request` | The typed request, identifier, enum, timestamp or required rationale/evidence is invalid. Correct it before retrying. |
| `revision_conflict` | Canonical state changed or a prepared snapshot is stale. Reread, reconcile and use a new operation ID where the logical input changes. |
| `operation_conflict` | An idempotency/event ID was already used with different input. Use the original input or a new ID. |
| `stale_claim` | A task lease/fencing token is absent, expired or replaced. Resume and reclaim/take over as allowed. |
| `capacity_exceeded` | A configured byte, count, depth, archive or batch bound was exceeded. Reduce the bounded request or change validated configuration. |
| `lock_timeout` | The repository mutation lock could not be acquired in time. Retry the same operation ID. |
| `storage_invalid` | Canonical, archive, journal or derived metadata failed integrity checks. Preserve files and inspect or rebuild the derived index. |
| `content_policy_rejected` | A direct or encoded secret pattern was detected recursively. Remove the sensitive value before retrying. |
| `approval_required` | A protected or high-risk action lacks an operator-managed grant for this repository, record revision and action. MCP tools cannot create grants. |
| `policy_denied` | The requested destructive behavior is disabled by repository policy; for example, compaction sources must be archived in this release. |
| `review_required` | A source has no explicit review decision and cannot be compacted. |
| `source_coverage_incomplete` | Compaction omitted a source, evidence reference or unresolved contradiction. |
| `validation_policy_failed` | A long-term candidate lacks independent confirmations or permitted authoritative evidence. |
| `event_type_reserved` | A caller attempted to write an authoritative lifecycle event; caller events must use `observation-*`. |
| `semantic_active_collection_missing` | No compatible active repository vector collection exists. Run controlled model migration/reindex. |
| `embedding_migration_required` | The configured embedding identity differs from the active collection. Run model migration before semantic recall. |
| `index_incomplete` / `index_incompatible` | Vector reconstruction or fingerprint/schema verification failed. Keep canonical files and repair/reindex. |

Invalid or expired cursors instruct full resynchronization. Dependency health reasons such as `dependency_unavailable`, `dependency_timeout`, `vector_index_unavailable` and `embedding_or_index_invalid` describe degraded recall/status; they do not mean canonical data was lost. After lock timeout or a lost connection, retry the same operation ID. Cancellation before a durable intent aborts; after it, recovery completes the commit. Cancelled MCP calls may have no response.

Actor IDs are trusted local provenance, not authentication. External output/evidence references are never dereferenced or inferred verified. Secret detection runs before staging but cannot prove content secret-free. Record conclusions and evidence, not hidden reasoning. Operator authorization grants for protected learning are external read-only inputs scoped to repository, record revision and action; caller approval claims never replace them and MCP tools cannot create them.
