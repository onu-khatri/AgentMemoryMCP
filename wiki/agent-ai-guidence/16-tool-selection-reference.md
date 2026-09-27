# All-tool selection reference

[Wiki index](README.md)

This page gives developers a compact decision map for every exposed tool. The tool's discovered input schema remains the source for exact fields.

## Session tools

| Tool | Use when | Do not use for | Real-life example |
| --- | --- | --- | --- |
| `list_agent_sessions` | The correct existing session ID is unknown | Loading session content | Find the prior `payments-migration` session after a new chat starts. |
| `create_or_activate_session` | A stable session ID has been selected | Reading prior work or setting global state | Activate `payments-migration` before all subsequent calls. |
| `resume_agent_session` | Starting, continuing, delegating or catching up | Acknowledging or mutating work | Load the current plan, completed outputs and active owners after restart. |
| `checkpoint_agent_session` | Every page of one snapshot was consumed | Saving a handoff or progress | Acknowledge sequence 42 after processing the complete snapshot. |
| `append_agent_memory` | Saving session context, decisions, findings, plans, outputs or handoffs | Task transitions or reusable learning | Save a test report output and update the shared next actions. |
| `coordinate_agent_task` | Creating, claiming, renewing, releasing, blocking, completing or reopening work | General artifacts or learning | Atomically claim the schema-migration task and later complete it with evidence. |

## Reusable-memory tools

| Tool | Use when | Do not use for | Real-life example |
| --- | --- | --- | --- |
| `memory_status` | Diagnosing counts, roots, backlogs and dependency health | Retrieving content | Confirm Qdrant is degraded before explaining lexical fallback. |
| `memory_remember` | Saving concise reusable temp, short or long-candidate learning | Full plans, transcripts or task ownership | Store a verified repository timestamp convention. |
| `memory_get` | An exact memory ID is known | Search or session resume | Inspect the full record returned by recall before using it. |
| `memory_update` | Replacing a temporary record with its current revision | Rewriting short or long learning | Refine a same-day incident hypothesis while shortening its expiry. |
| `memory_recall` | Searching bounded reusable learning | Treating memory as authority | Find up to five retry patterns for the payments subsystem. |
| `memory_list_review` | Inspecting bounded short-term review candidates | Performing lifecycle actions | List unreviewed records older than a release boundary. |
| `memory_mark_reviewed` | Recording a keep or candidate review decision | Actually compacting, promoting, archiving or deleting | Mark a verified rule as `promotion_candidate`. |
| `memory_prepare_compaction` | Freezing reviewed sources for agent-authored compaction | One-call compaction with an already prepared summary | Load five stable deployment notes and receive a preparation token. |
| `memory_commit_compaction` | Committing the reviewed representation for a preparation token | Generating a summary or skipping source coverage | Commit a checklist that preserves every source warning. |
| `memory_compact` | Summary and complete source coverage are already prepared | Exploring or reviewing sources | Compact two pre-reviewed duplicate records in one call. |
| `memory_archive` | Moving selected short records into verified compressed history | Age-based cleanup without review | Archive superseded investigation notes for audit. |
| `memory_delete` | Explicitly removing eligible temp/short records | Long-term removal or candidate marking | Delete a disposable non-protected temp note by revision. |
| `memory_record_event` | Recording replay-safe `observation-*` metadata | Outcomes, content or authoritative lifecycle | Record that release 42 was observed in staging. |
| `memory_record_outcome` | Reporting evidence-backed use of durable memory | General logs or task completion | Record that a retry lesson succeeded in another release. |
| `memory_promote` | Advancing temp, short or an eligible long candidate | Copying sessions or bypassing validation | Promote a reviewed short convention to a long candidate. |
| `memory_supersede` | Active long-term learning has a canonical replacement | Removal without replacement | Link the old retry policy to the new library-managed policy. |
| `memory_retire` | Long-term learning should leave ordinary recall | Physical deletion or replacement activation | Retire guidance for a removed subsystem. |
| `memory_cleanup` | Running bounded expiry/catalog maintenance | Semantic curation or session cleanup | Sweep expired temp records after a long offline period. |
| `memory_rebuild_indexes` | Reconstructing derived filesystem indexes | Qdrant or embedding migration | Repair a missing local catalog from canonical JSON records. |
| `memory_reindex` | Processing vectors, selected IDs, Qdrant or model migration | Editing canonical learning | Rebuild a deleted Qdrant collection from canonical long records. |

## Tool pair tests for custom agents

An agent should answer these distinctions correctly before production use:

- **Activate or resume?** Activate identity, then resume content.
- **Append or remember?** Append session-specific state; remember reusable learning.
- **Artifact or task transition?** Artifact for evidence/context; coordination for authoritative status.
- **Resume or checkpoint?** Resume reads; checkpoint acknowledges a complete read.
- **Get or recall?** Get exact ID; recall searches.
- **Review mark or lifecycle action?** Mark records intent; a separate tool performs it.
- **Event or outcome?** Event is observation metadata; outcome updates experience for a memory.
- **Rebuild indexes or reindex?** Rebuild is filesystem-only; reindex covers vector work and migration.
- **Supersede or retire?** Supersede names a replacement; retire removes ordinary eligibility without one.

## Real-life example: ambiguous “save what we learned” instruction

An agent finishes a parser task. It saves the current output, failed test details and next action with `append_agent_memory`. It extracts one broadly applicable verified parser convention and stores that with `memory_remember`. It completes the task with `coordinate_agent_task`. It does not put all three concerns into one memory record, so future agents can independently resume work, search reusable knowledge and trust task status.

