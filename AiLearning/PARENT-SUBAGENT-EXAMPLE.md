# Parent and sub-agent workflow

This example shows two agents sharing one durable session and promoting a reviewed result into advisory learning. Every call uses `arguments: { "request": { ... } }` unless the tool has no request object.

1. The parent calls `create_or_activate_session` for `parser-work`, then writes the reserved `context` artifact with `append_agent_memory`. It creates tasks with stable `workKey` values through `coordinate_agent_task`.
2. A child calls `resume_agent_session`, reads every page, and claims one task. The returned `claimToken` and `fencingGeneration` identify its current lease.
3. The child records findings or outputs with `append_agent_memory`, using the task's claim token and each artifact's current revision. It completes the task through `coordinate_agent_task` with output IDs, evidence and an explicit verification status.
4. The parent resumes again. It sees the child's completed task and reuses the recorded output instead of repeating the work. After reading every page, it calls `checkpoint_agent_session` with the exact snapshot ID and sequence.
5. Either agent may copy a concise, reusable result into short-term learning. Session data is not embedded or promoted automatically.

```json
{
  "request": {
    "operationId": "remember-parser-result-1",
    "tier": "short",
    "title": "Parser preserves source positions",
    "content": "The verified parser path preserves source positions for nested expressions.",
    "sessionId": "parser-work",
    "taskId": "parser-tests",
    "agentId": "test-agent",
    "category": "implementation-fact",
    "decisionArea": "parser",
    "tags": ["parser", "verified"],
    "sourceType": "session-output",
    "sourceReference": "artifact:parser-test-output"
  }
}
```

Later, an agent lists review candidates, inspects the canonical record, and marks it `keep`, `compact_candidate`, `promotion_candidate`, `archive_candidate` or `delete_candidate` with its current revision, evidence and rationale. Marking a candidate does not perform the lifecycle operation.

For compaction, call `memory_prepare_compaction` with at least two reviewed short-term IDs and revisions. Build a concise representation from the unchanged returned sources, then call `memory_commit_compaction` with the preparation token, all sources, per-source evidence and contradiction coverage, and the same source revisions. Originals are placed in a verified gzip archive as part of the recoverable commit. The server checks structure and coverage; the agent remains responsible for semantic accuracy.

Learning recall is advisory and may be degraded to lexical results while Ollama or Qdrant is unavailable. Session resume, task claims, contributions and checkpoints do not depend on either service.

