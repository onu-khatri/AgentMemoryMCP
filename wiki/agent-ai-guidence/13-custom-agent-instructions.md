# Custom agent instructions

[Wiki index](README.md)

Good agent instructions define tool boundaries, mandatory sequencing, recovery and stopping behavior. “Use memory when helpful” is too vague and produces inconsistent state.

## Copy-ready base instructions

```markdown
## Persisted memory and coordination

Use AgentMemoryMCP as the primary persisted memory source for this repository.
Current user decisions, repository instructions, source files and test evidence remain
authoritative over stored memory.

At the start of meaningful work:
1. Discover the existing session when its ID is unknown; do not create duplicates.
2. Activate the stable session ID.
3. Consume every resume_agent_session page before planning or repeating work.
4. Run a focused, bounded memory_recall when reusable learning may apply.

During work:
- Save objective, plans, decisions, findings, outputs and handoffs with
  append_agent_memory using current revisions and replay-safe operation IDs.
- Change task state only with coordinate_agent_task. Claim before task-owned side
  effects, retain the claim token, and renew long work.
- Save reusable cross-session facts and patterns with memory_remember. Do not copy
  full session state, transcripts, secrets or hidden reasoning into learning.
- Verify recalled advice against current evidence and record an outcome only after use.

Before stopping:
- Save outputs, evidence, blockers and a concrete handoff.
- Complete, block or release owned tasks explicitly.
- Resume again if concurrent changes may exist.
- Checkpoint only a fully consumed snapshot; checkpointing does not save work.

On conflicts or uncertainty:
- Retry an identical uncertain mutation with the same operation ID.
- On revision conflict, reread and reconcile with a new ID for changed content.
- On stale claim, stop owned writes and resume task state.
- On invalid cursor, start a full resync.
- Never bypass approval_required or content_policy_rejected.
```

## Role-specific additions

### Planner

- Save plans as `plan` artifacts with stable IDs.
- Keep the current plan explicit in `context.planReferences`.
- Create tasks with stable work keys, dependencies and acceptance expectations.
- Do not claim that a plan is approved without real evidence.

### Implementation agent

- Resume before editing.
- Claim the task and check claim ownership again before external side effects.
- Save non-obvious findings early enough for parallel agents to use.
- Complete with output references and actual validation evidence.

### Reviewer

- Resume the same session and inspect the implementation output and evidence.
- Record a separate decision or finding rather than rewriting the implementer's report.
- Use `passed`, `failed` or `partial` only when evidence was inspected.

### Curator

- Review canonical records before lifecycle actions.
- Preserve contradictions, open questions and source evidence during compaction.
- Never delete or promote solely because of age or similarity.

## Real-life example: repository code-review agent

A custom review agent receives a pull-request task. Its instructions make it resume the shared session, so it sees the implementation agent's output, acceptance expectations and exact test report. It claims the review task, records findings as task-linked artifacts and completes with `partial` because security tests were not run. It does not mark the implementation task failed, rewrite its output or save the whole diff as reusable memory. It remembers only a verified repository convention discovered during review.

## Keep instructions testable

Each rule should map to observable behavior: a tool call, an omitted unsafe call, a stored field or an error recovery. Avoid personality-only prose that cannot be evaluated.

