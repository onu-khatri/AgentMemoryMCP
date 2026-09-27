# Scenario cookbook

[Wiki index](README.md)

These scenarios can be copied into project instructions, agent examples or skill test fixtures.

## Scenario 1: continue yesterday's feature

**Trigger:** A developer asks an agent to continue a feature after the prior chat ended.

**Flow:** Discover the existing session if necessary, activate it, consume every resume page, load the active plan and unfinished tasks, recall focused module conventions, claim the next available task and continue. Save outputs and handoff before checkpointing.

**Avoid:** Creating a new session, asking the user to repeat known decisions, or treating activation as a resume.

**Real-life example:** The agent sees from `output-api-contract-tests` that the controller contract is already verified and works only on the remaining persistence task.

## Scenario 2: multiple plans in one session

**Trigger:** New evidence requires a revised implementation plan.

**Flow:** Resume the old plan and context revisions. In one `append_agent_memory` batch, mark the old plan's conventional data status as `superseded`, create the new plan and replace `context.planReferences` with the new ID.

**Avoid:** Choosing the latest plan by timestamp or putting full plans in reusable memory.

**Real-life example:** A zero-downtime migration plan replaces a maintenance-window plan after the user changes the deployment constraint.

## Scenario 3: parent delegates to two agents

**Trigger:** API implementation and integration tests can run independently.

**Flow:** Create tasks with distinct work keys, acceptance expectations and dependencies. Children resume and claim. Each saves task-linked outputs and completes with evidence. The parent resumes and integrates outputs.

**Avoid:** Assigning ownership only in prose or letting children edit the same artifact without revision reconciliation.

**Real-life example:** One agent implements webhook verification while another prepares test fixtures; a third task depends on both and cannot be claimed early.

## Scenario 4: an agent disappears with a claim

**Trigger:** The owning process stops and the lease expires.

**Flow:** Another agent resumes current task state, reads existing findings and outputs, explicitly claims after expiry and receives a new fencing token. It reconciles any external side effects before continuing.

**Avoid:** Reusing the prior token or assuming the server can undo filesystem edits made by the old owner.

**Real-life example:** A replacement agent finds a partially created migration, validates it against the database state and continues safely.

## Scenario 5: recurring investigation

**Trigger:** A failure resembles an earlier incident.

**Flow:** Resume current incident state, recall up to five records filtered by subsystem and failure category, verify them against current code, and reuse only applicable evidence. Record outcomes after testing.

**Avoid:** Loading every incident record or using semantic score as a correctness score.

**Real-life example:** A database timeout resembles a deadlock, but current telemetry shows connection-pool exhaustion; the agent records the deadlock advice as `not_applicable`.

## Scenario 6: semantic services are offline

**Trigger:** Qdrant or Ollama health is degraded.

**Flow:** Continue sessions, tasks and canonical learning writes. Use labeled lexical recall, disclose health reasons and leave pending vector work for maintenance.

**Avoid:** Claiming there were no long-term matches or inventing semantic similarity.

**Real-life example:** During a laptop Docker outage, two agents still coordinate a release and store a long candidate; it is indexed after Qdrant restarts.

## Scenario 7: consolidate repeated lessons

**Trigger:** Several reviewed short records repeat the same practice.

**Flow:** Prepare the exact sources, author a compact representation, preserve every source's evidence and contradictions, then commit and verify archive results.

**Avoid:** Merging merely similar conflicting claims or allowing age to authorize compaction.

**Real-life example:** Five deployment notes become one checklist while an unresolved rollback warning remains explicit.

## Scenario 8: memory advice becomes stale

**Trigger:** A dependency upgrade changes behavior.

**Flow:** Record `stale` or `contradicted` outcome with evidence, create and validate replacement learning, then supersede or retire the old record.

**Avoid:** Editing validated long-term content in place or physically deleting history.

**Real-life example:** A framework now handles retries internally, so the custom retry guidance is superseded.

## Scenario 9: protected deletion requested

**Trigger:** A user asks to remove an audit-sensitive record.

**Flow:** Inspect the record and revision, explain the required operator-managed grant and stop if `approval_required` is returned. Retry only after the external grant exists for that exact action and revision.

**Avoid:** Creating a grant through MCP, changing `protected`, or archiving as a deletion workaround.

**Real-life example:** A security incident record cannot be deleted based on an agent's statement that “the user approved it.”

## Scenario 10: conflict while updating context

**Trigger:** Two agents update next actions from the same context revision.

**Flow:** One succeeds; the other resumes, merges both non-conflicting contributions, preserves the new current plan and submits a new logical operation with the latest revision.

**Avoid:** Incrementing the revision manually or dropping the other agent's next action.

**Real-life example:** The API agent adds “run contract tests” while the database agent adds “apply migration fixture”; reconciliation retains both.

## Scenario 11: response lost after mutation

**Trigger:** A call disconnects after the server may have committed.

**Flow:** Retry the byte-equivalent logical input with the same operation ID. Accept the replayed committed result.

**Avoid:** New operation ID, reordered semantic content or duplicate artifact IDs.

**Real-life example:** The handoff batch is committed once even though the client sends it twice.

## Scenario 12: end a work session cleanly

**Trigger:** An agent is near its context or execution limit.

**Flow:** Save current findings, outputs, blockers and concrete next actions; release or block unfinished claims; remember only reusable verified lessons; resume concurrent changes; checkpoint the consumed snapshot.

**Avoid:** A prose-only farewell in chat or checkpointing before saving the handoff.

**Real-life example:** A new agent resumes immediately and runs the next listed command without asking what happened previously.

