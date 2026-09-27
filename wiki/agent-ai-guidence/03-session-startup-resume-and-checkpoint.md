# Session startup, resume and checkpoint

[Wiki index](README.md)

The session startup sequence is the foundation of restart continuity. Activation and resumption are separate operations: activation establishes the session identity, while resume loads a consistent snapshot.

## When the session ID is unknown

Call `list_agent_sessions` in bounded pages:

```json
{
  "pageSize": 25,
  "after": null
}
```

Continue with the returned `after` value while `hasMore` is true. The result contains identities, not session content. Choose an existing session from user intent, project instructions or a durable mapping; do not guess solely from a similar name.

## Activate

```json
{
  "sessionId": "payments-migration",
  "actorId": "implementation-agent",
  "operationId": "activate-payments-migration-implementation-agent"
}
```

If the response is lost, retry the identical logical request with the same operation ID. Activation does not set process-wide state, and later calls must continue to include the session ID.

## Resume every page

Start without a cursor:

```json
{
  "sessionId": "payments-migration",
  "actorId": "implementation-agent",
  "pageSize": 50,
  "cursor": null
}
```

Retain `snapshotId` and `sequence`, then follow the returned cursor until `hasMore` is false. A page may be smaller than requested because byte limits also apply. Do not merge pages from different snapshot IDs.

If a cursor is invalid or expired, discard the partial local view and start a full resynchronization without a cursor. Never checkpoint a partially consumed snapshot.

## What the agent should extract

Before acting, build a compact working view:

- objective and constraints;
- active plan references;
- decisions that affect the current step;
- completed tasks with outputs and evidence;
- active claims, owners and expiry;
- blockers and dependencies;
- handoffs and concrete next actions;
- unknown fields that require inspection rather than invention.

## Checkpoint

After consuming and applying every page:

```json
{
  "sessionId": "payments-migration",
  "actorId": "implementation-agent",
  "snapshotId": "snapshot-id-from-resume",
  "sequence": 42
}
```

Checkpointing only moves this actor's acknowledged sequence. It does not persist a finding, output or handoff. Save those first, then take a fresh snapshot if the agent must acknowledge its own contribution.

## Real-life example: restart during paginated resume

An agent receives page one of a large release session and then its process stops. It must not checkpoint page one. On restart, it activates the same session and starts resume without the stale cursor if that snapshot expired. It reads all pages, recognizes that deployment verification was already completed by another agent, reuses the referenced report and checkpoints the complete sequence. No work is repeated and no concurrent changes are skipped.

## Instruction snippet

```markdown
Never plan from an activation response. After activation, call resume_agent_session
and consume all pages from one snapshot. Treat unknown fields as unknown. Save any
new handoff before checkpointing. Checkpoint only the exact fully consumed snapshot.
```

