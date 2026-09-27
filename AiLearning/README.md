# Agent memory source documentation

This directory contains contracts, operating guidance and versioned schemas. The server never writes runtime memory into the checkout.

Runtime data defaults to `%USERPROFILE%/.codex/AgentMemory` on Windows and the equivalent user-profile path on other platforms. Set `SystemStorage__Root` to an absolute local path when an isolated root is needed, and always set a stable `Repository__Id`.

```text
<system-root>/
  sessions/<repository-id>/<session-id>/coordination/
  repositories/<repository-id>/AiLearning/
    temp/
    short-term/
    long-term/
    events/
    indexes/
    .operations/
  .vector/qdrant/
  .locks/
```

The JSON files under the repository learning directory are canonical. Filesystem indexes and repository-specific Qdrant collections are derived and rebuildable. A server process is bound to one repository identity; callers cannot select another repository in a tool request.

Start with:

- [MCP contract](MCP-CONTRACT.md) for tools, retry rules and typed examples.
- [Parent/sub-agent example](PARENT-SUBAGENT-EXAMPLE.md) for shared work and promoted learning.
- [Operations](OPERATIONS.md) for Qdrant, Ollama, backup, restore, rollback and reset.
- [Documentation index](INDEX.md) for schemas and change evidence.
- [Legacy reuse](LEGACY-REUSE.md) for breaking changes and historical-data handling.

Memory is advisory. Current code, tests, repository instructions, approved plans and explicit user decisions remain authoritative. Store concise facts, decisions, evidence and rationale. Do not store secrets or private chain-of-thought. Session artifacts remain durable when temporary learning expires or learning maintenance runs.

