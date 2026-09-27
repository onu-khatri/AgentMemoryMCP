# Purpose, users and product intent

[Developer wiki](README.md) | [Next: Solution architecture](02-solution-architecture.md)

## Problem being solved

AI agents lose operational continuity when a chat ends, a context window is compacted, a process restarts or work moves to another agent. Keeping the full history in every prompt is expensive and noisy. Plain notes do not solve concurrent ownership, evidence, lifecycle, semantic retrieval or crash safety.

This project supplies local persisted memory with two complementary responsibilities:

1. **Shared session continuity** records the active objective, plans, decisions, findings, outputs, handoffs, coordinated tasks and per-agent checkpoints.
2. **Reusable repository learning** records bounded temporary facts, reviewable short-term knowledge and evidence-backed long-term lessons.

The project aims to reduce repeated investigation and token cost while improving coordination. It does not attempt to replace source control, tests, issue tracking, authorization or current user direction.

## Primary users

- A single agent resuming work after its previous process stopped.
- Several agents collaborating in one session without repeating completed work.
- A parent agent delegating bounded tasks to children.
- A curation agent reviewing, compacting and promoting repository learning.
- A developer integrating the MCP server into custom agents, skills or project instructions.
- An operator maintaining local storage, Ollama and Qdrant.

## Product boundaries

| The server does | The server does not |
| --- | --- |
| Persist structured local session state | Persist private chain-of-thought |
| Enforce artifact revisions and task claim leases | Control filesystem or deployment side effects made by agents |
| Persist canonical learning with provenance | Treat remembered content as higher authority than current code |
| Provide lexical and semantic recall | Require semantic dependencies for session coordination |
| Recover interrupted multi-file commits | Automatically invent summaries or resolve contradictions |
| Require external grants for protected actions | Let MCP callers create their own grants |

## Why sessions and learning are separate

A plan, task owner or handoff is durable but meaningful inside one body of work. It should not appear as a general repository fact. A verified convention may help many future sessions and belongs in reusable learning. Keeping these domains separate prevents expired temp records from deleting task progress and prevents semantic recall from becoming an accidental task tracker.

## Expected developer perspective

When reading code, ask four questions:

1. Is this session state, canonical learning, or derived retrieval state?
2. Which revision or operation ID prevents ambiguity?
3. What happens if the process stops after this write?
4. Can the behavior continue while Ollama or Qdrant is unavailable?

These questions explain most implementation decisions.

## Example product journey

A developer asks an agent to continue a parser migration. `resume_agent_session` returns the current plan, completed test output, an unfinished task and a handoff. `memory_recall` supplies a verified parser convention from an earlier session. The agent verifies it against current code, claims the unfinished task, records its output and completes with evidence. A future agent resumes from persisted state without loading the original conversation.

## Prospective evolution

Likely extension areas include additional canonical storage backends, richer plan schemas, stronger identity/authentication, more retrieval strategies, remote transports and administrative tooling. Extensions must preserve repository isolation, canonical recoverability, idempotent mutations and honest degraded behavior.

