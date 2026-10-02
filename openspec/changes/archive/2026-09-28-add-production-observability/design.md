## Context

See [proposal.md](proposal.md) for motivation and [the capability spec](specs/mcp-observability/spec.md) for normative behavior.

The application is a .NET 10 generic-host executable using `ModelContextProtocol` 2.2.0 and stdio transport. `Program.cs` currently clears logging providers, sends all console levels to stderr, and registers 26 statically named tools. `McpProcess` already redirects both streams and parses every stdout line as JSON-RPC, providing a foundation for a stronger protocol-purity test. The server supports legacy initialization and the MCP `2026-07-28` stateless discovery model.

Tool methods delegate to service classes and translate selected domain exceptions at two duplicated `Invoke` helpers. Significant dependencies are an `HttpClient`-based Ollama client, a Qdrant gRPC client, bounded filesystem transactions/locks, and a background maintenance service. Only maintenance currently injects `ILogger`; there is no `ActivitySource`, `Meter`, OTLP exporter, centralized outcome classifier, or observability configuration. The MCP SDK version in use exposes incoming message filters and request-specific filters, including a `tools/call` filter, so cross-cutting instrumentation can be placed at the protocol boundary without changing every tool signature.

Existing storage requirements already prohibit rejected content from appearing in logs. This design extends that protection across every telemetry signal and queue. It must also preserve self-contained/single-file publication and all supported Windows, macOS, Linux, ARM64, and musl runtime targets.

## Goals / Non-Goals

**Goals:**

- Establish one versioned telemetry schema shared by logs, spans, metrics, tests, and operations documentation.
- Instrument protocol, tool, dependency, transaction, and maintenance boundaries once, with correct parentage and bounded outcome classification.
- Make safe metadata the only application-visible telemetry input; prevent raw request/result objects from reaching logging or OpenTelemetry APIs.
- Keep local execution useful with JSON diagnostics on stderr and no collector, while allowing production OTLP export through a vendor-neutral collector.
- Bound sampling, buffering, retries, rate limiting, signal size, and shutdown flush behavior.
- Produce repeatable automated and live verification evidence before production readiness is claimed.

**Non-Goals:**

- Adding Streamable HTTP, authentication, authorization, rate limiting, or new MCP tools.
- Changing MCP schemas, memory/session behavior, storage formats, or the current local-only dependency policy.
- Capturing prompts, tool arguments/results, memory records, session artifacts, hidden reasoning, or arbitrary baggage. This change exposes no content-capture switch; adding one requires a separate privacy review and OpenSpec change.
- Selecting or deploying a proprietary observability backend, defining organization-specific retention/RBAC, or hard-coding business SLO targets without operator approval.
- Treating telemetry as an audit system of record. Sampling-independent security events are operational evidence; regulated audit requirements remain a separate control plane.

## Decisions

### 1. Instrument MCP at SDK filter boundaries

Register an incoming MCP message filter to own the request activity, request counter, duration, in-flight count, and protocol-level outcome. Register a `tools/call` request filter inside it to own the tool activity, tool metrics, audit classification, and the single boundary completion/failure log. Use the SDK request context for the negotiated protocol version and the matched registered tool descriptor. Do not parse or serialize request arguments for telemetry.

The outer activity uses a stable name such as `mcp request` and the inner activity uses `mcp tool <configured-name>`. At startup, build the bounded tool registry from the same SDK descriptors that back `tools/list`; the matched descriptor is the only accepted request-time source for the tool-name dimension. Missing or unknown names map to `unknown`/`other`. A contract test compares the telemetry registry with `tools/list` so a separately maintained allowlist cannot drift. Other supported request types receive only the outer request span unless a future capability adds a meaningful inner operation.

For stdio, each request starts a new trace unless the active SDK exposes a documented, namespaced metadata field that the standard W3C propagator can consume. Malformed context is ignored safely. Tool arguments are never extended with hidden tracing fields. A future HTTP transport can reuse the same filters while normal HTTP instrumentation owns the server span and W3C headers; duplicate-span tests protect that transition.

General telemetry does not emit MCP request IDs, tool-call IDs, client identity, actor, operation, memory, session, or conversation IDs. Trace/span IDs are the correlation mechanism. The only actor exception is the explicitly approved pseudonymous actor field on operational security events. MCP logging notifications and deprecated `logging/setLevel` handling remain SDK protocol compatibility behavior; they neither carry application logs nor mutate provider configuration.

Alternative considered: add logging/timing to each tool wrapper. Rejected because it duplicates policy, misses protocol failures before method dispatch, and will drift as tools are added. Alternative considered: instrument only the raw message filter. Rejected because it cannot reliably distinguish tool lifecycle and domain outcomes without inspecting payloads.

### 2. Centralize schema, outcome mapping, and safe emission

Add an `Observability` area containing:

- validated `ObservabilityOptions` and startup validation;
- a versioned telemetry schema with event names, activity/meter names, attribute keys, allowed values, units, and maximum lengths;
- an outcome classifier mapping MCP/domain exceptions and cancellation state to `success`, `validation_error`, `authorization_denied`, `client_cancelled`, `deadline_exceeded`, `dependency_error`, `tool_error`, `protocol_error`, or `internal_error`;
- a request-scoped `TelemetryOutcomeContext` that carries only a typed classification through asynchronous execution, supports nested/concurrent calls, and is always restored or cleared when the tool filter completes;
- a typed `ClassifiedMcpException` derived from `McpException`, carrying only a registry-owned error code, bounded outcome, and optional validation/denial reason in addition to the existing safe client message; the tool `Invoke` helpers record the same classification into the active outcome context before throwing;
- a safe attribute builder that accepts typed operational fields only, truncates bounded strings, and never accepts arbitrary objects, headers, dictionaries, exception text, paths, or user content;
- source-generated structured log messages and a bounded per-event/tool/outcome rate limiter;
- small dependency/transaction helpers over `ActivitySource` and `Meter`.

The `tools/call` filter opens the typed outcome scope before invoking the SDK pipeline. The existing tool `Invoke` helpers remain responsible for public MCP error translation and preserve their current client-visible message, but they record the bounded classification into the active scope before throwing `ClassifiedMcpException`. Because the SDK converts an ordinary `McpException` to an error `CallToolResult` before the filter resumes, the filter consumes the recorded classification after `next` returns. An error result without a recorded classification maps to a fixed `tool_error`/`mcp_error` fallback. Exceptions that escape the SDK boundary, including cancellation and protocol failures, are classified directly. The context stores no exception, message, argument, result, or other content, and the filter clears or restores it in `finally`. Lower layers record span status/events but do not emit the same error log; the tool boundary owns one operational failure event.

Validation metrics use the fixed reason registry `missing_required`, `wrong_type`, `unknown_field`, `invalid_format`, `out_of_range`, `domain_rule`, and `other`. Authorization/policy denials use a separately bounded reason registry derived from explicitly mapped application codes such as `approval_required` and `policy_denied`; unknown codes map to `other`. Stable error codes may appear in approved logs/spans but are never unconstrained metric labels.

Alternative considered: catch `ClassifiedMcpException` in the tool filter. Rejected because the SDK converts ordinary `McpException` instances to error results before the filter regains control. Alternative considered: recover classification by parsing the error result or framework log text. Rejected because messages are not a stable schema, errors duplicate across layers, and privacy/cardinality controls would occur too late.

### 3. Use native .NET telemetry primitives with pinned OpenTelemetry packages

Domain and boundary instrumentation uses `System.Diagnostics.ActivitySource`, `System.Diagnostics.Metrics.Meter`, and `ILogger`; only host composition knows about the OpenTelemetry SDK. Add pinned, .NET 10-compatible OpenTelemetry hosting, OTLP exporter, and required client instrumentation packages. Package and semantic-convention upgrades are explicit reviewed changes with contract tests; experimental MCP/GenAI attributes are documented as experimental and kept behind the internal schema version.

Use one activity source and one meter named for `AgentSession.MCP`, versioned from the assembly. Standard resource/HTTP/RPC attributes take precedence where stable; custom MCP attributes use the documented `mcp.*` namespace and never duplicate stable standard fields merely for convenience. Do not adopt content-bearing GenAI attributes.

Alternative considered: vendor SDKs in services. Rejected because they couple domain code to one backend and split privacy controls. Alternative considered: logs only. Rejected because it cannot provide latency decomposition, reliable SLIs, or dependency parentage.

### 4. Define a bounded signal catalog

The initial meter catalog is:

| Instrument | Type/unit | Allowed dimensions |
|---|---|---|
| `mcp.server.requests` | Counter `{request}` | method, status, protocol_version, transport |
| `mcp.server.request.duration` | Histogram `s` | method, status |
| `mcp.server.inflight` | UpDownCounter `{request}` | method |
| `mcp.tool.invocations` | Counter `{invocation}` | tool_name, status |
| `mcp.tool.duration` | Histogram `s` | tool_name, status |
| `mcp.tool.input.items` | Histogram `{item}` | tool_name |
| `mcp.tool.output.items` | Histogram `{item}` | tool_name |
| `mcp.tool.input.size` | Histogram `By` | tool_name |
| `mcp.tool.output.size` | Histogram `By` | tool_name |
| `mcp.validation.failures` | Counter `{failure}` | tool_name, reason |
| `mcp.authorization.denials` | Counter `{denial}` | tool_name, reason |
| `mcp.protocol.errors` | Counter `{error}` | method, reason |
| `mcp.server.timeouts` | Counter `{timeout}` | method, tool_name |
| `mcp.server.cancelled` | Counter `{cancellation}` | method |
| `mcp.dependency.errors` | Counter `{error}` | dependency_type, operation |
| `mcp.logs.suppressed` | Counter `{log}` | event_name, reason |
| `mcp.telemetry.dropped` | Counter `{item}` | signal, reason |

Authorization/policy-denial instruments cover the controls already present in the application. A rate-limit rejection instrument is deferred until such a control exists; zero-value placeholder time series are not emitted. Input/output item histograms record only top-level argument-field and result-content-block counts available from parsed protocol objects without reading their values. Byte histograms use original protocol payload lengths only when exposed by the transport; instrumentation does not reserialize domain content solely to measure it. If exact size is unavailable, the byte measurement is omitted and the count remains available.

Histogram boundaries are explicit and versioned: request/tool/dependency latency uses sub-millisecond through timeout-scale buckets; payload sizes use powers-of-two-like buckets up to configured request limits. Tests enumerate every permitted label key and bounded value source. Metrics are unsampled.

### 5. Emit correlated JSON stderr and optional OTLP logs

Keep the console provider but select the built-in JSON formatter, UTC timestamps, scopes, and activity tracking for trace ID, span ID, and trace flags. Keep `LogToStandardErrorThreshold = Trace` so stdout remains reserved. A shared provider filter admits only the dedicated safe application event category plus a separately sanitized, rate-limited self-diagnostic category. General framework and SDK categories are excluded from both stderr and OTLP logging unless their category and event IDs receive an explicit schema review. Minimum levels remain configurable within the approved categories, with Information as the application default.

Register the OpenTelemetry logging provider only when remote log export is enabled. Source-generated constant message templates, registry-owned event IDs, and the typed safe-field builder create records shared by both providers. Provider filtering happens before formatting or queueing; formatted-message capture and automatic exception export are disabled. A final allowlist processor drops an entire unexpected record rather than forwarding unknown bodies or attributes. Stable `event.name` and typed fields drive queries, while the human message stays concise. The rate limiter key is composed only of schema-owned event name, configured tool/dependency, and bounded outcome, preventing attacker-controlled key growth. A periodic suppression summary is itself bounded and cannot recursively trigger suppression logs.

Alternative considered: send console output to a file tailing receiver. Rejected as the primary path because it weakens resource metadata and delivery semantics; stderr remains the local fallback, not the production export architecture.

### 6. Validate effective configuration and resource identity

Introduce an `Observability` configuration section for application policy (enabled signals, exporter enabled/protocol, sampling, timeouts, batch/queue/flush bounds, size limits, log-rate limits, environment, and schema policy). Support standard `OTEL_*` environment variables for exporter/resource settings, with documented precedence and validation of the effective result. Secrets such as exporter headers are read only by the exporter and are never rendered in startup summaries or validation messages.

Resource identity includes:

- `service.name = agent-session-mcp` by default, never `unknown_service`;
- service namespace and assembly/package version;
- a per-process instance ID that is not used as a metric label;
- deployment environment and .NET runtime identity;
- telemetry schema/policy version;
- explicit application schema version and a deterministic toolset version computed from canonical sorted registered tool descriptors and their public schemas;
- transport as `stdio`, with negotiated MCP version attached at request scope.

Remote export is disabled by default, so development requires no collector. A local activity listener/provider stays registered whenever observability is enabled, even with no exporter, so JSON stderr records retain trace/span correlation. If export is enabled, endpoint URI, protocol, sampling ratio, finite timeouts, queue/batch sizes, and flush budget validate on start. Clear-text OTLP is permitted only for loopback or a same-host sidecar endpoint; every non-loopback endpoint requires TLS and no endpoint may embed credentials, query, or fragments. Configuration validity is separate from reachability: a syntactically valid but unavailable collector degrades at runtime instead of blocking server startup.

### 7. Combine high-level manual dependency spans with selective auto-instrumentation

Add high-level spans around Ollama model discovery/embedding, Qdrant collection/query/upsert operations, repository lock acquisition, managed transaction commit/recovery, bounded maintenance passes, and vector reconciliation/migration. Attributes identify only dependency class and bounded operation; collection names, model digests, memory/session IDs, paths, and content are excluded. File spans cover meaningful transactions and recovery, not each file API call.

Enable `HttpClient` instrumentation for the Ollama network request, with header/query capture disabled and a filter restricted to the configured loopback client. The high-level Ollama span explains the logical operation while the child HTTP client span explains network latency. Qdrant gets a high-level client span around its service wrapper. Automatic gRPC instrumentation remains explicitly disabled in the initial implementation because the logical wrapper spans provide stable, sanitized coverage without depending on client-library span behavior; it may be enabled only if a later integration test proves it produces one useful, sanitized child span without duplicate high-level operations.

Timeouts, caller cancellations, retries, and final dependency errors are classified separately. Existing exception messages are not attached wholesale. Retry count/reason use bounded fields, and successful retries do not produce one alerting error event per attempt.

### 8. Batch export without coupling request success to telemetry

Use batch processors/exporters with bounded queues and finite export timeouts. Preserve parent sampling with a parent-based sampler. The local provider/listener creates correlation activities independently of whether an OTLP exporter is configured; sampling controls recording/export while never changing MCP results. The normal production topology sends unsampled application metrics and the configured trace/log stream to a nearby collector; a gateway collector may perform tail sampling to retain failures, slow operations, canaries, selected high-value tools, and a healthy baseline. A constrained deployment may use head sampling, with documentation that traces discarded at the process cannot be recovered by tail sampling.

Application policy/suppression drops and failed export batches increment bounded counters where the process can observe them. SDK self-diagnostics use a guarded stderr path and are not fed recursively back into the same failing exporter. Collector refusal, queue, retry, memory, and drop metrics remain the source of truth after OTLP acceptance. Host shutdown disposes providers within a finite configured budget; failure to flush never blocks process exit indefinitely.

Alternative considered: synchronous exporting from the filter. Rejected because collector/backend latency would become MCP latency. Alternative considered: unbounded retries for loss prevention. Rejected because it can exhaust memory and make telemetry failure a service outage.

### 9. Ship a hardened collector baseline and backend-neutral operations contract

Add an `observability/` deployment area with a pinned Collector image/config and an optional Compose profile separate from the existing Qdrant service. In the container, the collector listens on `0.0.0.0` for OTLP gRPC/HTTP so Docker port forwarding works; Compose publishes those ports only on host `127.0.0.1` by default. Non-container examples bind directly to loopback. The collector applies `memory_limiter` first, deletes defense-in-depth sensitive attributes, performs configured tail sampling where enabled, batches, and exports over authenticated TLS to an operator-supplied OTLP backend. Sending queues and retries are finite. Persistent file storage is an opt-in overlay with filesystem protection and retention warnings, not the default.

Collector telemetry exposes accepted/refused/dropped records, exporter failures, queue size/capacity, retry activity, CPU, memory, and persistent storage usage when enabled. The repository documents six backend-neutral dashboard views: service health, tools, protocol/version, dependencies, security/audit, and telemetry pipeline. Each panel is defined by signal, dimensions, aggregation, and diagnostic purpose so it can be translated to the selected backend. PromQL or vendor-specific examples may be supplementary but are not the contract.

SLO documentation defines eligible request populations so validation failures, denials, and caller cancellations are not silently counted as server availability failures. Exact objectives, alert thresholds, retention, RBAC, and backend credentials are deployment inputs. The repository provides conservative starting examples clearly labeled for operator approval, plus runbooks linked from every sustained alert.

### 10. Verify at signal, process, collector, and release levels

Add unit tests using in-memory exporters/listeners for schema allowlists, registered-tool equality, application/toolset version determinism, typed MCP error translation, bounded validation/denial reasons, resource identity, outcome mapping, log category/event rejection, log rate limiting, truncation, label bounds, cancellation, conditional byte measurement, safe count measurement, and exception sanitization. Golden records assert structure and absence rather than snapshotting unstable timestamps/IDs. A generated synthetic-secret corpus is passed through request metadata, nested content, dependency failures, and exceptions; all captured signals and stderr are scanned.

Extend process tests to exercise successful, invalid, cancelled, and dependency-failure calls while parsing every stdout line as JSON-RPC and every diagnostic stderr line as bounded JSON. Integration tests assert request/tool/dependency span parentage, trace/log correlation, metrics, no duplicate tool spans, exporter outage behavior, shutdown budget, and queue/drop behavior. A real pinned collector smoke/failure test validates the checked-in configuration; backend credentials are not required because a local sink can inspect OTLP output.

Add a repeatable load/benchmark harness that compares instrumentation disabled, local-only, and OTLP batch-export modes. Record p50/p95/p99 latency, throughput, CPU, allocations, queue memory, and emitted bytes per request, then commit an approved regression budget before release gating. CI runs deterministic unit/process tests on supported host families and clearly labels Docker/live collector and full RID publication checks; skipped checks are reported as unverified.

## Risks / Trade-offs

- **[SDK filter context or protocol metadata changes across package versions]** → Pin the MCP and OpenTelemetry packages, isolate SDK adaptation in filters, and run contract tests for every supported protocol version before upgrades.
- **[Sensitive values leak through exception text or framework auto-instrumentation]** → Use typed allowlists, disable header/query/content capture, sanitize before logging, suppress exception messages by default, add collector defense-in-depth deletion, and scan all signals with canary secrets.
- **[High-cardinality labels create cost or memory growth]** → Source dimensions from static registries/enums, map unknowns to `other`, enumerate label keys in tests, and keep request/actor/session IDs off metrics.
- **[Instrumentation adds latency or allocations]** → Avoid payload reserialization and fine-grained file spans, use source-generated logs and batch export, benchmark three modes, and gate against an explicit budget.
- **[Collector/backend outage causes recursive diagnostics or memory pressure]** → Bound queues/retries/timeouts, separate guarded self-diagnostics from OTLP logging, expose drops, and failure-inject unavailable/full collectors.
- **[Tail sampling consumes collector memory]** → Size policies against traffic, put the memory limiter first, expose queue/memory telemetry, and allow documented head-sampling fallback with its loss trade-off.
- **[Single-file/trimming or cross-platform behavior breaks after adding exporters]** → Publish and smoke-test representative Windows, Linux, Linux-musl, macOS, x64, and ARM64 outputs before production claims.
- **[Audit events are mistaken for a compliance-grade immutable audit trail]** → Label them operational security telemetry, document sampling/retention boundaries, and keep regulated audit requirements out of this capability.
- **[Experimental MCP/GenAI semantic conventions drift]** → Pin and document the convention policy, prefer stable standard attributes, version custom attributes, and review changes as compatibility work.

## Migration Plan

1. Add pinned packages, observability options/schema, JSON stderr logging, and in-memory tests with OTLP export disabled by default. Existing local startup behavior remains available.
2. Register MCP filters and dependency/maintenance instrumentation, then run the full unit/process suite, stdout-purity checks, secret scans, publication smoke tests, and baseline benchmarks.
3. Add and validate the pinned collector configuration, local sink smoke tests, outage/full-queue tests, dashboards, alerts, SLO definitions, privacy/retention guidance, and runbooks.
4. Deploy the collector and instrumented server to a non-production environment in shadow mode. Validate resource identity, cardinality, trace parentage, redaction, queue sizing, overhead, and shutdown behavior before enabling production export.
5. Roll out production export gradually, beginning with metrics and a bounded trace/log sample. Operators approve backend TLS/auth, retention/RBAC, SLO targets, alert thresholds, and any persistent queue.
6. Roll back by disabling remote signal export while retaining structured stderr and in-process instrumentation. If necessary, revert the package/config change; no memory or session data migration is involved.

## Open Questions

- Which OTLP backend and authentication mechanism will each production environment use? The collector contract deliberately leaves these as secret-managed deployment inputs.
- What SLO objectives, alert thresholds, telemetry retention, and access roles will operators approve after observing representative non-production traffic? The signal definitions and task breakdown do not depend on those values.
