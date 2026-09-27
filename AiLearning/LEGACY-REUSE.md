# Requirement-driven reuse

The active server uses central, repository-scoped structured session storage. Historical Markdown/YAML sessions are not read, imported, rewritten or deleted. This is a breaking contract change.

| Previous surface | Decision and replacement | Requirement / verification |
| --- | --- | --- |
| `list_agent_sessions`, `create_or_activate_session` | Adapt names to bounded discovery and explicit shared session IDs under the central root. No implicit process-wide active session. | Central repository-scoped sessions; `McpSessionTests` |
| `append_agent_memory` | Replace freeform append with atomic structured batches, actor, revision and operation ID. | Shared contributions; `SessionCoordinationTests`, `McpSessionTests` |
| `read_agent_memory` | Replace with consistent, paginated `resume_agent_session` and explicit checkpoints. | Restart-safe resumption; `SessionCoordinationTests`, `McpSessionTests` |
| `create_agent_artifact`, `read_agent_artifact`, `list_agent_artifacts` | Replace generic path/name writers with typed artifact contributions and resume entries. Plans and outputs remain supported artifact kinds. | Reserved coordination state; protocol discovery tests |
| `save_final_plan`, `get_latest_final_plan` | Replace timestamp-selected plans with stable plan artifacts, expected revisions and explicit current plan references in shared context. Claimed approval is provenance, never an authorization grant. | Resumption and explicit current state; structured context protocol test |
| `log_agent_event` | Remove arbitrary freeform log writes. Coordination emits server-owned change records; sanitized learning events are a separate pending task. | No raw logging sink; reserved task transitions |
| `FileAgentSessionStore`, legacy lifecycle/memory/artifact/plan facade, YAML models and contracts | Remove. They cannot provide repository containment, task fencing or transactional visibility. | Managed-store recovery, cross-process locking and protocol tests |
| `SessionStoragePathBuilder`, old `SessionStorageOptions` | Replace with bound root/repository options and strict managed resolver. No compatibility path aliases. | `MemoryConfigurationTests`, `ManagedStoragePathTests` |
| `SystemFileSystem` atomic writes | Retain and strengthen strict UTF-8, flush, cancellation and staging cleanup. | `AtomicFileTests` |
| Identifier safety predicate | Retain strict validation; never transform a supplied repository/session ID into a different ID. | Configuration and path tests |
| YamlDotNet, YAML serialization/front-matter helpers, obsolete DTOs and tests | Remove with the unused legacy persistence route. New JSON/protocol tests cover the supported contracts. | Versioned JSON and new workflow tests |
| MCP SDK, Generic Host, HttpClientFactory | Retain; pin MCP to stable 2.2.0, use one explicit registration path, propagate cancellation and keep stdout JSON-RPC only. | Protocol tests across 2025-06-18, 2025-11-25 and 2026-07-28 |

Rollback requires restoring the prior executable and its prior configuration. The prior release does not understand the new central JSON coordination records. Do not point it at the new root or assume a downgrade converts records. Back up central data before upgrading or rolling back. No migration tool is included because no historical import requirement was confirmed.
