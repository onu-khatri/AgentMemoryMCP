# Observability operations ownership

AgentMemoryMCP consumes the local `OnuObservability.Hosting` and `OnuObservability.Mcp` package family. Package-owned configuration, signals, privacy policy, adapters, and collector behavior are documented in the sibling [`OnuObservability/docs`](../../../OnuObservability/docs/README.md); this page covers service operation only.

[Previous: Developer readiness](23-developer-readiness-checklist.md) | [Developer wiki](README.md)

The canonical operational content is maintained under [docs/observability](../../docs/observability/README.md):

- configuration and signal ownership: `configuration-and-signals.md`;
- diagnostic views and provisional reliability policy: `dashboards-and-slos.md`;
- incident procedures: `runbooks.md`;
- privacy, retention, access, and production approvals: `privacy-and-production-checklist.md`;
- release evidence and explicit unverified scope: `verification.md`.

This wiki owns developer navigation and source architecture only. It links to those files rather than copying procedures, preventing conflicting operational instructions.
