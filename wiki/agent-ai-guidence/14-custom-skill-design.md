# Custom skill design

[Wiki index](README.md)

A skill should encode a repeatable workflow around the MCP tools rather than merely list tool names. Define triggers, prerequisites, read sequence, write ownership, error recovery and completion evidence.

## Skill contract template

```markdown
# Skill name

## Trigger
Use when ...

## Required inputs
- Stable session ID or rules for discovering it
- Actor ID
- User objective and relevant repository scope

## Read phase
1. Activate the session.
2. Resume every page.
3. Extract objective, active plan, task state, evidence and unknowns.
4. Recall at most N reusable records using named filters.

## Work phase
1. Claim or create the task with acceptance expectations.
2. Perform the bounded work.
3. Save findings and outputs with current revisions.
4. Recheck ownership before external side effects.

## Completion phase
1. Run the specified validation.
2. Complete, block or release the task with evidence.
3. Save a handoff and reusable lessons where appropriate.
4. Resume concurrent changes and checkpoint the complete snapshot.

## Error rules
- Same operation ID for identical retry.
- Reread and reconcile revision conflicts.
- Stop on stale claim or required operator grant.
- Full resync for invalid cursor.
```

## Skill design practices

### Separate reads from writes

Make the initial read phase explicit. Skills that mix discovery and mutation often create duplicate sessions, use stale revisions or overwrite another agent's contribution.

### State the persistence boundary

Name what remains only in live context, what becomes a session artifact and what qualifies as reusable memory. Default to session storage for work-specific material.

### Generate stable IDs

Derive IDs from semantic scope, such as `finding-order-lock-order`, rather than random timestamps. Generate a fresh operation ID for each new logical mutation and retain it for retries.

### Require evidence

The completion phase should define commands or observations that justify verification status. A skill should not infer `passed` from a tool call returning without error.

### Bound recall

Specify query shape, decision area, category/tags and maximum results. Broad recall can cost more tokens than it saves and can surface unrelated advice.

### Avoid autonomous curation

Implementation skills may remember a concise verified fact, but compaction, archival, deletion and long-term validation should live in dedicated curation skills with stronger review requirements.

## Real-life example: feature implementation skill

A `feature-implementation` skill activates the issue session, resumes it, resolves the active plan, recalls at most five records tagged with the affected module and claims the implementation task. It saves a finding when an API contract differs from the plan, updates the plan only after reconciliation, records an output containing changed paths and test evidence, and completes the task. It saves one short-term lesson about a verified module convention. If tests fail, it completes with `failed` only when the task is truly finished with a failed result; otherwise it blocks the task with the failure and next diagnostic action.

## Real-life example: session handoff skill

A `handoff` skill does not summarize the entire chat. It resumes current state, inspects owned tasks, saves one structured handoff per relevant scope, releases or blocks unfinished claims, records output references, resumes once more for concurrent updates and checkpoints. The next agent receives operational facts rather than a long narrative.

## Skill review questions

- Can the skill resume after the model process stops at any step?
- Can two copies run without silent overwrite?
- Does it distinguish session data from reusable learning?
- Does it preserve evidence and unknowns?
- Are retries idempotent?
- Does it stop when authorization or ownership is missing?
- Can another agent continue without the original conversation?

