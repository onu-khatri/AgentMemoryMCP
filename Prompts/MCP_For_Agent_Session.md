# Agent Memory MCP usage prompt

Use AgentSession.MCP as the primary persisted memory source for repository work. Keep only the immediate working set in prompt context. Retrieve prior work before repeating it, and persist concise structured state that another agent can resume after restart.

Persisted memory supports continuity and coordination. It remains advisory: current code, tests, repository instructions, approvals and explicit user decisions are authoritative.

## Start or resume work

1. If the session ID is unknown, call `list_agent_sessions` until `hasMore` is false and select the existing session that matches the work.
2. Call `create_or_activate_session` with a stable `sessionId`, `actorId` and replay-safe `operationId`. This establishes session identity; it does not load prior work.
3. Call `resume_agent_session`. Follow its cursor until `hasMore` is false. Read the objective, plans, decisions, findings, outputs, task owners, blockers, handoffs and changes before planning or repeating work.
4. Call `memory_recall` with a focused query, relevant filters and a small `maxResults` when reusable learning may apply. Use `memory_get` only for an exact known record or to inspect a recalled record in full.
5. Verify recalled advice against the current repository and user direction.

## Save the right kind of memory

Use `append_agent_memory` for session-specific operational state shared by agents:

- the current objective and constraints;
- plan references and concrete next actions;
- decisions and their rationale or evidence;
- findings that prevent repeated investigation;
- outputs and validation evidence;
- handoffs that identify completed work, blockers and the next owner action.

Use `memory_remember` for concise reusable learning that should be found in later sessions:

- verified facts with evidence;
- successful or failed patterns;
- repository conventions and operational lessons;
- short-lived working facts in `temp`;
- durable reviewable knowledge in `short`;
- unvalidated long-term proposals in `long_candidate`.

Do not store full transcripts, disposable intermediate reasoning, private chain-of-thought, credentials or secrets. Do not copy every session artifact into reusable memory.

## Coordinate shared work

Use `coordinate_agent_task` as the only authoritative path to create, claim, renew, release, block, complete or reopen a task. Do not encode ownership or task transitions only in artifacts. Recheck the current claim before external side effects because claims cannot fence filesystem or other tool actions.

Use `append_agent_memory` to save supporting findings, outputs and handoffs. Task-linked artifacts require the current claim token. Completion should include concrete outputs or a handoff and evidence for any verification claim.

## Curate reusable learning

- Use `memory_record_outcome` to record evidence-backed results for a durable record. It is reliability feedback, not a general log.
- Use `memory_record_event` only for replay-safe `observation-*` metadata. It cannot perform task or memory lifecycle transitions.
- Use `memory_list_review` to inspect candidates and `memory_mark_reviewed` to record a review decision. A candidate mark does not compact, promote, archive or delete anything.
- Use `memory_prepare_compaction` followed by `memory_commit_compaction` when the agent must inspect a stable source set before authoring the summary. Use `memory_compact` only when the complete reviewed summary and source coverage are already prepared.
- Use `memory_promote`, `memory_supersede` and `memory_retire` for their explicit lifecycle operations. Long-term memory is never physically deleted through `memory_delete`.
- Use `memory_archive` or `memory_delete` only after an explicit review decision and with required operator-managed grants. MCP tools cannot create grants.

## Finish or hand off

Before stopping:

1. Save current context, findings, outputs and a concrete handoff with `append_agent_memory`.
2. Complete, block or release owned tasks through `coordinate_agent_task` with evidence and next actions.
3. Save broadly reusable lessons with `memory_remember` and attach real outcomes when applicable.
4. Resume again if concurrent contributions may have arrived.
5. Call `checkpoint_agent_session` only after every page of the snapshot has been consumed. Checkpointing acknowledges a read position; it does not save work.

For exact schemas, failure handling and examples, use `AiLearning/MCP-CONTRACT.md` and `AiLearning/PARENT-SUBAGENT-EXAMPLE.md`.
