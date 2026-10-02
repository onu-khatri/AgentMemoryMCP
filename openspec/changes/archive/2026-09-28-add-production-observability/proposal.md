## Why

The stdio MCP server currently has only basic console logging, so operators cannot reliably correlate protocol requests, tool execution, dependency latency, failures, or telemetry-pipeline health without risking sensitive-data exposure. Production deployment needs vendor-neutral, privacy-first observability that preserves protocol correctness and continues serving requests when telemetry infrastructure is unavailable.

## What Changes

- Add configurable structured logging and OpenTelemetry traces, metrics, and logs with consistent service/resource identity and trace/log correlation.
- Instrument MCP message and tool-call boundaries plus Ollama, Qdrant, filesystem, maintenance, and lifecycle operations with stable, low-cardinality names and bounded outcome classifications.
- Enforce metadata-only telemetry by default: raw tool arguments, results, memory content, prompts, credentials, paths, resource identifiers, and hidden reasoning are excluded; safe fields are allowlisted and bounded before emission.
- Preserve stdio safety by keeping stdout protocol-only and routing local diagnostics to structured stderr output; telemetry export and shutdown flushing remain bounded and fail open for MCP serving.
- Provide validated configuration for exporters, sampling, limits, and environment/resource attributes, with secure production defaults and an explicit opt-in boundary for any future content capture.
- Add a vendor-neutral OpenTelemetry Collector deployment baseline with memory protection, batching, queues, retry, redaction, TLS/auth placeholders, and collector self-monitoring guidance.
- Add automated contract, redaction, cardinality, trace/metric/log, stdout-purity, collector-outage, cancellation, and performance-overhead verification.
- Document the signal catalog, SLOs, dashboards, alerts, retention/access expectations, and operational runbooks for server, tool, dependency, and telemetry-pipeline incidents.

## Capabilities

### New Capabilities

- `mcp-observability`: Production-safe structured logging, tracing, metrics, privacy controls, exporter resilience, collector deployment, verification, and operational readiness for the MCP server.

### Modified Capabilities

None. Existing MCP tool contracts, memory behavior, storage safety, and session coordination requirements remain unchanged.

## Impact

- Affected runtime areas: `AgentSession.MCP/Program.cs`, host/service registration, MCP SDK request filters, tool execution boundaries, dependency clients, background maintenance, and shutdown behavior.
- Affected configuration and dependencies: new OpenTelemetry .NET packages and strongly validated observability options; OTLP endpoints and resource identity become configurable without requiring a vendor SDK.
- Affected operations: collector configuration/deployment assets, dashboards/alerts/SLO guidance, telemetry retention and access policy, and incident runbooks.
- Affected tests: `AgentSession.MCP.Tests` gains in-memory telemetry assertions, secret-leakage and cardinality checks, protocol stdout checks, exporter/collector failure injection, and bounded-overhead verification.
- Compatibility: no MCP tool schema or storage-format breaking change; stdio remains the only enabled transport, and HTTP propagation is documented as a future-compatible extension point rather than added by this change.
