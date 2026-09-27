# Outcomes and long-term lifecycle

[Wiki index](README.md)

Long-term learning becomes trustworthy through evidence-backed use, independent confirmation and explicit lifecycle actions. Retrieval frequency alone is not proof.

## Record an outcome

```json
{
  "memoryId": "long-memory-id",
  "outcomeId": "orders-release-20260927-retry-result",
  "expectedRevision": 3,
  "sessionId": "orders-release-2026-09",
  "taskId": "verify-bounded-retry",
  "agentId": "release-agent",
  "result": "success",
  "evidence": ["trx:orders-retry-tests", "log:release-probe-42"],
  "notes": "The bounded retry prevented duplicate processing under the verified outage fixture."
}
```

Outcome kinds are `success`, `partial_success`, `failure`, `contradicted`, `not_applicable` and `stale`. `outcomeId` is replay-safe. Independent successes must differ by session, agent and evidence; repeating one outcome cannot manufacture confirmations.

## Validate a candidate

A candidate needs the configured independent-success policy or permitted authoritative evidence, followed by explicit `memory_promote`. High-risk validation additionally requires an operator-managed grant scoped to repository, record revision and action. MCP tools cannot create grants.

The server maintains advisory counters and confidence. Agents should show the evidence and lifecycle state rather than presenting the numeric confidence as certainty.

## Supersede when a replacement exists

```json
{
  "memoryId": "old-long-memory-id",
  "operationId": "supersede-old-retry-rule",
  "expectedRevision": 7,
  "agentId": "architecture-agent",
  "reason": "The new client library implements jitter internally",
  "evidence": ["commit:abc123", "test:retry-policy-v2"],
  "replacementMemoryId": "new-long-memory-id"
}
```

`memory_supersede` preserves bidirectional history and identifies the active replacement.

## Retire without replacement

Use `memory_retire` when learning is no longer ordinarily valid but no canonical replacement exists. It removes the record from ordinary recall without physical deletion. An optional replacement link may still be retained.

## Observation events are different

`memory_record_event` stores replay-safe metadata-only `observation-*` events. It cannot impersonate lifecycle transitions:

```json
{
  "eventId": "observation-release-42",
  "eventType": "observation-release-completed",
  "agentId": "release-agent",
  "memoryId": "long-memory-id",
  "sessionId": "orders-release-2026-09",
  "taskId": "verify-bounded-retry",
  "metadata": {
    "environment": "staging",
    "release": "42"
  }
}
```

An observation is not an outcome and does not change validation, retirement or supersession state.

## Real-life example: library upgrade invalidates a validated pattern

A validated memory says callers must wrap the HTTP client in a custom retry loop. After a library upgrade, tests show the library now retries internally and the extra loop causes duplicate requests. The upgrade agent records a `contradicted` outcome with test evidence, creates a new candidate describing the built-in policy, and supersedes the old record after the replacement meets policy. Future ordinary recall excludes the old advice, while historical retrieval still explains why earlier code used it.

