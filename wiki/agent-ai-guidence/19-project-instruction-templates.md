# Project instruction templates

[Wiki index](README.md)

Project instructions establish behavior shared by every agent and skill in a repository. Keep them shorter than a full skill, but make the mandatory memory sequence and authority rules explicit.

## Minimal repository policy

```markdown
## AgentMemoryMCP

Use AgentMemoryMCP as the primary persisted memory source for meaningful repository
work. Existing source, tests, project instructions and explicit user decisions remain
authoritative over stored memory.

Before continuing known work, activate its stable session and consume every
resume_agent_session page. Use focused memory_recall queries before repeating prior
research. Save session-specific plans, decisions, findings, outputs and handoffs with
append_agent_memory. Save only concise reusable learning with memory_remember.

Use coordinate_agent_task for every task transition. Claim before task-owned side
effects and preserve evidence for completion. Checkpoint only after consuming the
full snapshot; checkpointing does not save work.

Never store secrets, full transcripts or hidden reasoning. On revision conflict,
reread and reconcile. Retry an identical uncertain mutation with the same operation
ID. Never bypass stale claims, content-policy rejection or operator-managed grants.
```

## Multi-agent repository extension

```markdown
When work can be parallelized, the parent creates non-overlapping coordinated tasks
with stable work keys, dependencies and acceptance expectations. Child agents resume
the same session, claim one task, attach findings and outputs using the claim token,
and complete, block or release explicitly. Parents resume and reuse child outputs;
they do not repeat completed work merely to rebuild context.

Recheck current ownership before filesystem, deployment or external tool effects.
Claims coordinate agents but cannot fence effects outside AgentMemoryMCP.
```

## Plan-management extension

```markdown
Store each implementation plan as a plan artifact with a stable versioned ID. Select
the current plan through context.planReferences. When replacing a plan, atomically
update the old plan's informational status, create the new plan and update context.
Plan status in artifact data is a project convention, not an enforced task lifecycle.
Use coordinate_agent_task for actual work status.
```

Recommended project plan-status convention:

| Value | Meaning |
| --- | --- |
| `draft` | Under development and not selected in `planReferences`. |
| `active` | Selected as current by shared context. |
| `completed` | Its intended work and validation are complete. |
| `superseded` | Replaced by another named plan. |
| `abandoned` | Intentionally stopped with a recorded reason. |

Because the server does not enforce these plan values, define them once in project instructions and avoid agent-specific synonyms.

## Learning-policy extension

```markdown
Use temp memory only for restart continuity within its bounded lifetime. Use short
memory for reviewable verified facts and patterns. Use long_candidate only for a
proposed durable lesson with provenance. Recalled memory is advisory and must be
checked against current repository evidence.

Agents may record evidence-backed outcomes after real use. Only dedicated curation
workflows may compact, archive, delete, validate, supersede or retire durable memory.
Age and similarity alone never authorize a lifecycle action.
```

## Module-specific extension

Add concrete filters and evidence standards for a module:

```markdown
For payment work, use decisionArea `payments` or a narrower `payments-*` value and
tag records with the provider or protocol. Verification claims require an integration
test report or named external probe. Recall at most five payment records by default.
Do not store customer payloads, provider credentials or raw webhook bodies.
```

## Choosing a session ID

Define a deterministic convention appropriate to the repository:

- issue work: `issue-<number>-<short-name>`;
- release work: `release-<version>`;
- incident work: `incident-<date>-<short-name>`;
- long-running initiative: `<initiative-name>`.

Do not embed filesystem paths, user names or secrets. Do not generate a new ID when project metadata already maps the work to an existing session.

## Real-life example: standardizing a platform repository

A platform team adds the minimal, multi-agent and plan-management blocks to its root instructions. It adds a module extension requiring `decisionArea=identity` and integration-test evidence for authentication claims. Planning, implementation and review agents now share one vocabulary for plan status, reuse the same issue session, and produce compatible handoffs. A new skill inherits these repository rules rather than inventing its own session naming or memory policy.

## Maintenance guidance

Review project instructions when tool contracts or repository governance changes. Keep universal rules here and detailed workflows in skills. If a project rule conflicts with current tool schemas, update the rule rather than teaching agents to submit obsolete payloads.

