# Agent AI guidance for AgentMemoryMCP

This wiki helps developers design custom agents, skills and repository instructions that use AgentSession.MCP as their primary persisted memory. The goal is to keep live model context focused while preserving restart continuity, multi-agent awareness, evidence and reusable learning.

The server stores two different kinds of information:

- **Session state** records what agents are doing now: objectives, plans, decisions, findings, outputs, handoffs, task ownership and checkpoints.
- **Reusable learning** records facts and patterns that may help future sessions: temporary observations, reviewable short-term knowledge and validated long-term learning.

Persisted memory is advisory. Current source files, tests, repository instructions, explicit approvals and user decisions remain authoritative.

## Reading paths

For a developer integrating the server for the first time:

1. [Architecture and mental model](01-architecture-and-mental-model.md)
2. [Configuration and client integration](02-configuration-and-client-integration.md)
3. [Session startup, resume and checkpoint](03-session-startup-resume-and-checkpoint.md)
4. [Session artifacts, plans and handoffs](04-session-artifacts-plans-and-handoffs.md)
5. [Multi-agent task coordination](05-multi-agent-task-coordination.md)
6. [Custom agent instructions](13-custom-agent-instructions.md)
7. [Custom skill design](14-custom-skill-design.md)
8. [Project instruction templates](19-project-instruction-templates.md)

For memory and retrieval behavior:

- [Reusable memory tiers](06-reusable-memory-tiers.md)
- [Recall and token-efficient context](07-recall-and-token-efficiency.md)
- [Review, compaction, archive and deletion](08-review-compaction-archive-and-deletion.md)
- [Outcomes and long-term lifecycle](09-outcomes-and-long-term-lifecycle.md)

For production use:

- [Errors, retries and concurrency](10-errors-retries-and-concurrency.md)
- [Security, privacy and authorization](11-security-privacy-and-authorization.md)
- [Operations and semantic dependencies](12-operations-and-semantic-dependencies.md)
- [Scenario cookbook](15-scenario-cookbook.md)
- [All-tool selection reference](16-tool-selection-reference.md)
- [Agent and skill evaluation checklist](17-agent-and-skill-evaluation.md)
- [External standards and references](18-external-references.md)
- [Project instruction templates](19-project-instruction-templates.md)

## Core agent loop

Every custom agent should follow this loop:

1. Discover the existing session if its ID is unknown.
2. Activate the stable session ID.
3. Resume every page before planning or repeating work.
4. Recall a small, filtered set of reusable learning when it can help.
5. Claim shared tasks before performing their side effects.
6. Persist decisions, findings, outputs and handoffs as structured session artifacts.
7. Persist only genuinely reusable conclusions in a learning tier.
8. Reconcile concurrent changes instead of overwriting them.
9. Checkpoint only after the full snapshot has been consumed.

## Real-life example: agent returns after a weekend

An implementation agent is asked to continue a payment-provider migration. It does not paste three days of conversation into its prompt. It activates `payments-migration`, resumes the session through the last page, sees that another agent finished webhook verification, loads the referenced test output, recalls the repository's retry convention, claims only the remaining reconciliation task and continues from that evidence. Before stopping, it records its output and a handoff, completes the claimed task, and checkpoints the consumed snapshot.

That workflow reduces repeated exploration and prompt cost without allowing old memory to override the current codebase.

## Contract notation

Examples show the object passed to a tool's `request` parameter. Raw MCP JSON-RPC clients wrap it as `arguments: { "request": { ... } }`. Tools with no request object, such as `memory_status`, are called without one. JSON fields are camelCase and enum values are snake_case.
