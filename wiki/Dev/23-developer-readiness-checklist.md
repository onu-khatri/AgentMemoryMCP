# Developer readiness checklist

[Previous: Glossary](22-glossary.md) | [Developer wiki](README.md)

A developer is ready to own the project when they can complete the following without relying on undocumented knowledge.

## Product understanding

- [ ] Explain the token-cost, restart and multi-agent problems being solved.
- [ ] Distinguish session state, reusable learning and derived retrieval state.
- [ ] Explain why memory is advisory.
- [ ] Give examples of content that must remain outside persisted memory.

## Architecture

- [ ] Draw the request path from MCP client through tool, service, transaction and response.
- [ ] Explain why one process binds one repository ID.
- [ ] Explain why canonical files own truth and Qdrant is rebuildable.
- [ ] Locate every production service using the source catalog.

## Durability and concurrency

- [ ] Explain operation ID versus revision.
- [ ] Walk through intent, mutations, receipt and recovery after a crash.
- [ ] Explain the in-process and OS portions of the repository lock.
- [ ] Explain task leases, claim tokens and fencing generations.
- [ ] Explain why claims cannot fence external side effects.

## Session workflows

- [ ] Demo activation followed by full paginated resume.
- [ ] Save context and multiple plans with correct revisions.
- [ ] Create, claim, renew, complete, block and reopen tasks.
- [ ] Explain snapshot consistency and checkpoint restrictions.

## Learning workflows

- [ ] Create and retrieve temp/short/long-candidate records.
- [ ] Explain temp expiry and why only temp supports in-place update.
- [ ] Demo focused recall and lexical degradation.
- [ ] Explain review versus lifecycle actions.
- [ ] Walk through preparation/commit compaction and archive verification.
- [ ] Explain outcomes, independent confirmations and confidence.
- [ ] Explain promote, supersede and retire.

## Semantic subsystem

- [ ] Explain projection version and embedding fingerprint.
- [ ] Explain deterministic point IDs and payload filters.
- [ ] Trace pending work through `VectorIndexCoordinator`.
- [ ] Explain model migration, stable alias switch, recovery and rollback.
- [ ] Rebuild Qdrant from canonical records.

## Security and operations

- [ ] Explain path containment and reparse-point rejection.
- [ ] Explain recursive content-policy limits and limitations.
- [ ] Explain external operator grants and why tools cannot create them.
- [ ] Run dependency checks, backup/restore and scoped derived-index reset.
- [ ] Diagnose every stable tool error in the troubleshooting guide.

## Testing and delivery

- [ ] Run standard tests and report exact pass/skip totals.
- [ ] Run live semantic tests with isolated dependencies.
- [ ] Explain fault injection and cross-process test workers.
- [ ] Publish and run MCP smoke tests on the target runtime.
- [ ] Update schemas, consumer docs, developer flows and acceptance evidence with a contract change.

## Final exercise

Starting with an empty chat and a running server, perform the complete demo in [Build, run and demonstration](20-build-run-and-demo.md). Then explain each persisted file category, each service involved, what would happen if the process crashed at the durable intent, and how the system recovers if Qdrant is deleted.

