# Recall and token-efficient context

[Wiki index](README.md)

The server reduces prompt cost when agents retrieve a bounded working set instead of copying all prior conversations into every request. Retrieval quality depends on focused queries, metadata filters and disciplined result handling.

## Two reads with different purposes

- `resume_agent_session` retrieves authoritative operational state for one shared session.
- `memory_recall` searches advisory reusable learning across eligible tiers in the bound repository.

Always resume the active session. Recall learning when prior facts or patterns could reduce repeated work.

## Focused semantic recall

```json
{
  "query": "How are webhook retries made idempotent in this repository?",
  "tiers": ["short", "long"],
  "decisionAreas": ["payments-webhooks"],
  "categories": ["implementation-fact", "successful-pattern"],
  "tags": ["webhook"],
  "maxResults": 5,
  "minimumSimilarity": 0.55,
  "includeCandidates": false,
  "includeHistory": false,
  "includeArchived": false,
  "includeRetired": false
}
```

Use a question that names the decision or task, not a one-word query. Start with a small result bound. Add session, task, parent-step, agent, category, decision-area, tags, status, memory IDs or time filters when they express real intent.

An empty query returns recent eligible records. Use it for “what changed recently?” rather than pretending it is semantic search.

## Results

Each result includes the canonical record, `matchType`, an optional score and `advisory: true`. Semantic and lexical scores are not interchangeable. During Ollama or Qdrant outages, the result mode and health reasons disclose degraded lexical behavior; no fake semantic score is produced.

Use `memory_get` when an exact ID is known or the full canonical record is needed:

```json
{
  "memoryId": "memory-id-from-recall",
  "includeArchived": false,
  "includeHistory": false
}
```

Do not call `memory_get` repeatedly as a substitute for search.

## Context assembly pattern

After resume and recall, keep only:

1. Current objective and non-negotiable constraints.
2. The active plan step.
3. Relevant current task ownership.
4. Completed outputs that the step depends on.
5. At most a small set of applicable learning records with provenance.
6. Open uncertainties and the next action.

Summarize references; do not paste every record verbatim. Retain IDs so the agent can fetch details if needed.

## When to report an outcome

If an agent uses a durable record and observes whether it worked, it should record an evidence-backed outcome. This improves reliability metadata and enables long-term validation. Mere retrieval is not success.

## Real-life example: diagnosing a recurring deadlock

An agent resumes `order-deadlock-incident` and sees the current reproduction steps. It recalls five results filtered to `decisionArea=database-concurrency`. One long-term result describes a bounded retry convention, while a short result records that a specific transaction must not retry. The agent verifies both against current code and chooses the applicable rule. It keeps the two record IDs and key conclusions in context instead of loading months of incident notes. After a test proves the fix, it records a success outcome for the applicable memory and a `not_applicable` outcome for the unrelated one.

## Agent instruction snippet

```markdown
Use focused memory_recall queries before repeating repository research. Default to
five or fewer results and apply decision-area, category, tag, task or time filters.
Treat match scores as retrieval signals, not truth. Verify against current sources,
keep record IDs for provenance, and record an outcome only after observable use.
```

