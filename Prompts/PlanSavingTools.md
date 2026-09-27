# Plan persistence with Agent Memory MCP

Save implementation plans as revision-controlled `plan` artifacts through `append_agent_memory`. Reference the current plan artifact ID from the shared `context.planReferences` list so every agent resumes the intended plan rather than guessing from timestamps.

## Save a plan

1. Activate the stable session and call `resume_agent_session` through the final page.
2. Reconcile the existing plan artifact and shared-context revisions.
3. Call `append_agent_memory` with a structured `plan` artifact. Include acceptance criteria, ordered steps, constraints, validation, evidence references and the authoring actor where applicable.
4. Update the `context` artifact in the same atomic batch when the new plan should become current. Put the plan artifact ID in `context.planReferences` and record concrete next actions.
5. Reuse the same `operationId` only when retrying the identical logical request. After a revision conflict, resume, reconcile and use a new operation ID for changed content.

## Read the current plan

Call `resume_agent_session` and consume all pages. Resolve `context.planReferences` against the returned plan artifacts. The explicit reference determines the current plan; filename order and timestamps do not.

Use `memory_remember` only for reusable learning extracted from a plan, such as a verified repository convention. Do not store the plan itself as general reusable memory, and do not mark a plan as approved unless the evidence represents a real external approval.

The obsolete `save_final_plan` and `get_latest_final_plan` tools are not exposed. For exact request schemas and concurrency rules, use `AiLearning/MCP-CONTRACT.md`.
