# Review, compaction, archive and deletion

[Wiki index](README.md)

Short-term learning remains reviewable. Age can suggest review but never authorizes deletion, archival, compaction or promotion.

## List review candidates

```json
{
  "maxResults": 20,
  "olderThanUtc": "2026-09-01T00:00:00Z",
  "reviewState": "unreviewed",
  "category": "implementation-fact",
  "decisionArea": "payments-webhooks"
}
```

Inspect each candidate with `memory_get` and verify evidence before deciding.

## Mark a review decision

```json
{
  "memoryId": "short-memory-id",
  "operationId": "review-webhook-rule",
  "expectedRevision": 1,
  "agentId": "curation-agent",
  "action": "promotion_candidate",
  "reason": "Verified in two release branches and useful beyond this session",
  "evidence": ["trx:branch-a", "trx:branch-b"]
}
```

Allowed `memory_mark_reviewed` actions are `keep`, `compact_candidate`, `promotion_candidate`, `archive_candidate` and `delete_candidate`. `unreviewed` is the initial state and may be used as a list filter; it is not a mark action. Marking a candidate performs no lifecycle operation.

## Prepare and commit compaction

Use the two-step path when the agent needs a stable source snapshot:

```json
{
  "operationId": "prepare-webhook-compaction",
  "agentId": "curation-agent",
  "sources": [
    { "memoryId": "memory-a", "expectedRevision": 2 },
    { "memoryId": "memory-b", "expectedRevision": 4 }
  ]
}
```

The result contains unchanged sources and a `preparationToken`. The agent authors a concise representation without losing evidence, open questions or contradictions, then calls `memory_commit_compaction` with:

- the returned token;
- the same source IDs and revisions;
- summary, facts and open questions;
- successful and failed patterns;
- all contradictions and evidence;
- one `sourceCoverage` item per source;
- a reason and replay-safe operation ID.

Use `memory_compact` only when the complete reviewed representation and coverage are already prepared. The server checks structural coverage and archives originals, but the agent remains responsible for semantic accuracy.

## Archive and delete

`memory_archive` creates and verifies a bounded JSONL+gzip archive before removing active short-term copies:

```json
{
  "operationId": "archive-obsolete-webhook-notes",
  "agentId": "curation-agent",
  "reason": "Superseded notes retained for audit",
  "records": [
    { "memoryId": "memory-c", "expectedRevision": 3 }
  ]
}
```

`memory_delete` physically removes only temp or short memory and requires an explicit actor, reason and revision. Protected records need operator-managed grants. Long-term learning is retired rather than deleted.

## Real-life example: consolidate five deployment notes

Five reviewed short records describe the same deployment sequence, but one contains a warning about a rollback race. A curator prepares all five sources, writes one compact summary, preserves the warning in `contradictions` or `openQuestions`, maps every source's evidence in `sourceCoverage`, and commits. The originals enter a verified archive. The curator does not automatically merge a sixth record that recommends the opposite sequence; it remains separate until the contradiction is resolved.

## Skill rule

A curation skill must never equate “old,” “similar” or “low confidence” with permission to delete. It should separate inspect, decide and execute phases and require explicit evidence for each transition.
