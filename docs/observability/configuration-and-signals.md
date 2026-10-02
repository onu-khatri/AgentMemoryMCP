# AgentMemory observability compatibility and application schema

Reusable options, resource fields, MCP request/tool signals, safe log events, histogram views, export health, privacy rules, and shutdown behavior are owned by the package references:

- [OnuObservability configuration](../../../OnuObservability/docs/configuration.md)
- [OnuObservability signals](../../../OnuObservability/docs/signals.md)
- [MCP adapter guide](../../../OnuObservability/docs/adapters.md)
- [Application-schema guidance](../../../OnuObservability/docs/application-schema.md)

AgentMemoryMCP currently pins `OnuObservability.Hosting` and `OnuObservability.Mcp` `0.1.0-alpha.9` from the repository-local feed. The package-native configuration root is `OnuObservability`.

## Legacy configuration compatibility

Existing deployments may continue using the `Observability` section during the prerelease migration window. Startup translates only known values into `OnuObservability` settings. Explicit package-native values take precedence, followed by supported standard `OTEL_*` variables according to package policy.

The compatibility layer maps common switches, service identity, OTLP endpoint/protocol, sampling, exporter/batch/queue/shutdown limits, logging bounds, rate limits, and insecure sidecar hosts. These renamed limits map as follows:

| Legacy setting | Package setting |
|---|---|
| `Observability:MaxExportBatchSize` | `OnuObservability:MaximumExportBatchSize` |
| `Observability:MaxLogBodyLength` | `OnuObservability:MaximumLogBodyLength` |
| `Observability:MaxAttributeLength` | `OnuObservability:MaximumAttributeLength` |
| `Observability:MaxLogRateLimitKeys` | `OnuObservability:MaximumLogRateLimitKeys` |
| `Observability:MaxToolArgumentFields` | `OnuObservability:Mcp:MaximumInputItems` |
| `Observability:MaxResultContentItems` | `OnuObservability:Mcp:MaximumOutputItems` |

AgentMemory defaults `OnuObservability:LocalJsonLoggingEnabled=true` and `OnuObservability:Mcp:StdioSafeLogging=true` so bounded JSON diagnostics use stderr and stdout remains MCP protocol traffic. New deployments should use package-native keys; removal of the legacy translation requires a separately versioned compatibility change.

## AgentMemory-owned descriptors

The consumer registers a finite application schema for these dependency and maintenance boundaries:

- Ollama: `model_discovery`, `embedding`;
- Qdrant: `health`, `collection`, `migration`, `query`, `upsert`, `delete`;
- filesystem: `lock`, `transaction`, `recovery`;
- maintenance: `migration`, `maintenance`, `reconciliation`, `reindex`;
- one `other/other` fallback for each client/internal boundary.

Allowed fields remain `dependency.type`, `dependency.operation`, bounded `mcp.batch.items`, bounded aggregate `mcp.work.*` counts, and `mcp.recovery.performed`. Unknown type/operation combinations fail closed to `other`; record content, identifiers, paths, URLs, request/result bodies, and exception messages are never emitted.

The application retains the compatibility activity source name `AgentSession.MCP` and established `mcp dependency ...` / `mcp operation ...` span names. The package owns the `mcp.dependency.errors` instrument through `IMcpDependencyFailureRecorder`, avoiding a second local MCP meter.

## Service identity and operations

AgentMemory defaults remain:

- service name: `agent-session-mcp`;
- service namespace: `agent-memory`;
- remote export: disabled;
- OTLP logs: disabled unless explicitly enabled;
- stdio-safe local JSON: enabled.

Use [dashboards and SLOs](dashboards-and-slos.md), [runbooks](runbooks.md), [privacy checklist](privacy-and-production-checklist.md), and [verification evidence](verification.md) for service-specific operation. Disable remote export with `Observability__ExporterEnabled=false` or the package-native equivalent without changing MCP behavior.
