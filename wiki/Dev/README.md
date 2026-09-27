# AgentMemoryMCP developer wiki

This wiki onboards a developer from product purpose through implementation, operations and demonstration. It is organized so a new maintainer can explain why the system exists, trace every MCP tool to persisted data, find the class that owns a behavior, change the design safely and demonstrate the application without relying on another developer.

All source links point to files without line numbers so the wiki remains stable as code moves.

## Recommended learning path

1. [Purpose, users and product intent](01-purpose-users-and-product-intent.md)
2. [Solution architecture](02-solution-architecture.md)
3. [Startup, dependency injection and configuration](03-startup-configuration-and-dependency-injection.md)
4. [Storage, concurrency and recoverable transactions](04-storage-concurrency-and-transactions.md)
5. [Session tool end-to-end flows](05-session-tool-flows.md)
6. [Core memory tool end-to-end flows](06-core-memory-tool-flows.md)
7. [Curation tool end-to-end flows](07-curation-tool-flows.md)
8. [Learning lifecycle tool end-to-end flows](08-learning-lifecycle-tool-flows.md)
9. [Maintenance tool end-to-end flows](09-maintenance-tool-flows.md)
10. [Semantic pipeline: Ollama and Qdrant](10-semantic-pipeline.md)
11. [Domain models and contracts](11-domain-models-and-contracts.md)
12. [Security and trust boundaries](12-security-and-trust-boundaries.md)
13. [Hosted maintenance and status](13-hosted-maintenance-and-status.md)
14. [Testing architecture and acceptance evidence](14-testing-and-acceptance.md)
15. [Production source catalog](15-production-source-catalog.md)
16. [Test and operations source catalog](16-test-and-operations-source-catalog.md)
17. [Technology and design decisions](17-technology-and-design-decisions.md)
18. [Extension and change guide](18-extension-and-change-guide.md)
19. [Troubleshooting and failure diagnosis](19-troubleshooting.md)
20. [Build, run and demonstration script](20-build-run-and-demo.md)
21. [Project mind map](21-project-mind-map.md)
22. [Glossary](22-glossary.md)
23. [Developer readiness checklist](23-developer-readiness-checklist.md)

## The one-sentence architecture

AgentSession.MCP is a .NET 10 stdio MCP server that persists repository-isolated shared sessions and tiered learning as canonical local files, coordinates concurrent agents with revisions and leases, and uses local Ollama embeddings plus a rebuildable Qdrant index for semantic recall.

## Invariants every maintainer must know

- The process binds one stable repository identity at startup.
- Session artifacts and reusable learning are different persistence domains.
- Canonical files are recoverable truth; catalogs, embeddings and vectors are derived.
- Every multi-file mutation is revision checked, idempotent and roll-forward recoverable.
- A task claim coordinates agents but cannot fence side effects outside the server.
- Recalled memory is advisory; current repository evidence and user decisions remain authoritative.
- Temp memory expires within 24 hours; age alone never authorizes durable-memory deletion.
- MCP tools cannot create operator authorization grants.
- Stdio stdout is protocol-only; logs go to stderr.

## Companion documentation

- [Agent and skill integration guidance](../agent-ai-guidence/README.md)
- [MCP contract](../../AiLearning/MCP-CONTRACT.md)
- [Operations guide](../../AiLearning/OPERATIONS.md)
- [OpenSpec design](../../openspec/changes/productionize-agent-memory/design.md)
- [Acceptance verification](../../openspec/changes/productionize-agent-memory/verification.md)

## First demonstration

Build the solution, start the server with a stable `Repository__Id`, discover 26 tools, activate a session, save context, resume it after restarting the process, create and recall a short memory, then show `memory_status`. The complete script and expected observations are in [Build, run and demonstration](20-build-run-and-demo.md).

