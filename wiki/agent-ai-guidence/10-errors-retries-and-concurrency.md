# Errors, retries and concurrency

[Wiki index](README.md)

Agents must distinguish transport uncertainty from logical conflict. Blind retries can duplicate intent, overwrite concurrent work or repeatedly submit an invalid request.

## Retry matrix

| Error or condition | Agent response |
| --- | --- |
| Lost response or `lock_timeout` after a mutation | Retry the identical request with the same `operationId`. |
| `operation_conflict` | The ID was reused with different input. Recover the original request or choose a new ID for the new logical operation. |
| `revision_conflict` | Resume/read current state, reconcile differences and submit changed content with a new operation ID and current revision. |
| `stale_claim` | Stop owned side effects, resume task state and claim or take over only if allowed. Never reuse the stale token. |
| Invalid or expired cursor | Restart resume without a cursor and consume a new complete snapshot. |
| `capacity_exceeded` | Reduce page size, batch size or payload while preserving semantic completeness. |
| `content_policy_rejected` | Remove the sensitive value; do not encode or split it to evade detection. |
| `approval_required` | Stop the protected action and obtain an external operator grant. MCP tools cannot create it. |
| `storage_invalid` | Preserve data and request operator inspection. Do not overwrite canonical files. |
| Dependency health reason | Continue canonical/session work and disclose degraded recall; retry bounded maintenance later. |

## Operation ID discipline

An operation ID identifies one logical mutation, not one network attempt.

Good patterns:

- `save-parser-finding-v1`
- `complete-webhook-tests-attempt-1`
- `remember-clock-skew-rule`

If payload content changes after reconciliation, the logical mutation changed and needs a new operation ID. If only the response was lost, keep the same ID.

## Revision discipline

Every mutable artifact or memory record has a revision. Never cache a revision indefinitely. Resume or get the record shortly before a write, especially when multiple agents are active.

An artifact update is full replacement, so reconciliation must preserve fields written by other agents. Do not fix a conflict by incrementing the revision number without reading the current record.

## Cancellation

Cancellation before a durable intent aborts the operation. After durable intent, recovery may complete the commit even if the caller receives no response. Treat a cancelled or disconnected mutation as uncertain: retry the identical operation ID to discover the committed result.

## Real-life example: response lost after a three-artifact batch

An agent atomically updates context, creates a finding and creates an output. The connection drops before it sees the result. It does not generate a new operation ID or reconstruct a slightly different batch. It sends the exact same request with the same ID and receives the original committed sequence and artifact revisions. If it had changed the output title under the same ID, the server would return `operation_conflict` instead of creating ambiguous history.

## Instruction snippet

```markdown
Treat operationId as the identity of one logical write. Retry identical uncertain
writes with the same ID. On revision conflict, reread and reconcile; never guess the
new revision. On stale claim, stop task-owned writes. On invalid cursor, perform a
full resync. Preserve canonical data when storage integrity is uncertain.
```

