# Reusable memory tiers

[Wiki index](README.md)

Reusable memory is separate from session artifacts. Copy only a concise conclusion that is likely to help later work, and retain provenance back to the session, task or output that supports it.

## Tier selection

| Tier on `memory_remember` | Use for | Avoid |
| --- | --- | --- |
| `temp` | Restart continuity for short-lived working facts; expires no later than 24 hours | Durable conventions, important decisions or anything that must survive expiry |
| `short` | Reviewable facts, patterns and lessons that may help future sessions | Unverified speculation presented as fact |
| `long_candidate` | A proposed durable lesson ready to collect outcomes and pass validation policy | Directly claiming the lesson is validated |

Recall uses tiers `temp`, `short` and `long`; `long_candidate` is a creation choice, while candidate/validated is the long record's status.

## Remember a reusable fact

```json
{
  "operationId": "remember-webhook-clock-skew-rule",
  "tier": "short",
  "title": "Webhook verification permits five minutes of clock skew",
  "content": "The verified webhook path accepts timestamps within five minutes of server UTC and rejects older requests.",
  "sessionId": "payments-migration",
  "taskId": "webhook-tests",
  "agentId": "test-agent",
  "category": "implementation-fact",
  "decisionArea": "payments-webhooks",
  "tags": ["webhook", "security", "verified"],
  "sourceType": "session-output",
  "sourceReference": "artifact:output-webhook-test-report",
  "structuredData": {
    "allowedSkewSeconds": 300,
    "evidenceType": "integration-test"
  },
  "reviewAfterUtc": "2026-10-27T00:00:00Z",
  "protected": false
}
```

The request's optional `repositoryId` cannot switch the bound repository. Prefer omitting it unless a client needs an explicit consistency check.

## Update only temporary memory

`memory_update` replaces a temp record using `expectedRevision` and `operationId`. It preserves creation time and may shorten, never extend, expiry. Durable short and long-term learning changes through review, compaction, promotion, supersession or retirement.

```json
{
  "memoryId": "memory-id",
  "operationId": "refine-temp-investigation-note",
  "expectedRevision": 1,
  "agentId": "diagnostic-agent",
  "content": "The failure is isolated to requests with an absent tenant header.",
  "title": "Tenant-header investigation",
  "tags": ["incident", "tenant"],
  "expiresAtUtc": "2026-09-27T20:00:00Z"
}
```

## Promotion is explicit

Promotion can advance temp to short, short to a long candidate, or an eligible long candidate to validated state. It preserves provenance; it does not copy an entire session automatically.

```json
{
  "memoryId": "memory-id",
  "operationId": "promote-webhook-rule",
  "expectedRevision": 2,
  "agentId": "curation-agent",
  "reason": "The rule is reusable and supported by integration evidence",
  "authoritativeVerification": false,
  "verificationEvidence": ["trx:webhook-tests-20260927"]
}
```

## What never belongs in reusable memory

- Full chat transcripts.
- Hidden chain-of-thought or scratch reasoning.
- Credentials, tokens, private keys or connection strings.
- A task's current owner or lease.
- A full plan that is relevant only to one session.
- An unverified guess without its uncertainty and provenance.

## Real-life example: incident hypothesis becomes a durable lesson

During an outage, an agent stores a temp note that missing tenant headers may cause cache pollution. Tests confirm the cause, so the agent updates the session finding and records a concise short-term learning with evidence. Later sessions report independent successful outcomes when the guard prevents recurrence. Only after the validation policy is met does a curator promote the long candidate. The original incident transcript and debugging dead ends never enter durable learning.

