# MCP Observability Specification

## Purpose

Provide production-safe observability for the MCP server so operators can diagnose request, tool, dependency, security, and telemetry-pipeline behavior without corrupting protocol traffic or exposing private content.

## Requirements

### Requirement: Protocol-safe structured logging
The server SHALL emit structured, event-oriented application logs with stable event names, severity, UTC timestamp, service identity, deployment environment, and safe outcome metadata. Logs written during an active span SHALL carry trace ID, span ID, and trace flags. For stdio transport, stdout SHALL contain only MCP protocol traffic and local application diagnostics SHALL use stderr; logs SHALL NOT be returned in MCP responses or expose internal stack traces to clients. Local and remotely exported logs SHALL admit only approved application categories, event IDs, bodies, and attributes before formatting or queueing; arbitrary framework or SDK records SHALL be excluded unless separately reviewed and sanitized. Repeated events SHALL be rate-limited with a suppression count, and log bodies and attributes SHALL have configured size bounds. MCP logging notifications and the deprecated `logging/setLevel` operation SHALL NOT be the application observability control plane.

#### Scenario: Correlated tool completion log
- **WHEN** a tool call completes while a trace span is active
- **THEN** one completion event is emitted with the tool name, bounded status, duration, trace ID, and span ID but without raw request or result content

#### Scenario: Stdio remains parseable
- **WHEN** the server starts, handles successful and failed requests, exports telemetry, and shuts down over stdio
- **THEN** every stdout frame is valid MCP protocol traffic and all local structured diagnostics are isolated from stdout

#### Scenario: Repeated failure logs are suppressed safely
- **WHEN** equivalent malformed calls exceed the configured logging rate limit
- **THEN** detailed events are bounded and a suppression counter records the number omitted without recording malformed values

#### Scenario: Unapproved logger category emits a record
- **WHEN** a framework, SDK, or application category outside the reviewed event allowlist writes a log
- **THEN** the record is excluded from stderr and remote export without inspecting or exporting its body or exception

### Requirement: Correlated MCP and dependency traces
The server SHALL create or continue a bounded trace for each MCP request and SHALL create a distinct internal tool-operation span for each `tools/call` without duplicating an equivalent transport span. Spans SHALL use predictable low-cardinality names and record the negotiated protocol version, transport, method, tool name where applicable, status classification, duration, and safe size/count metadata. Meaningful Ollama, Qdrant, filesystem transaction, maintenance, and other dependency operations SHALL appear as child spans with appropriate client or internal span kinds. Valid W3C context SHALL be honored where the transport and protocol metadata support it; absent or malformed context SHALL start an independent trace without failing the MCP request or changing public tool arguments.

#### Scenario: Tool call trace hierarchy
- **WHEN** a tool call invokes Ollama or Qdrant
- **THEN** the exported trace contains a request span, one tool-operation span, and dependency child spans with correct parentage and no raw content in span names or attributes

#### Scenario: Stdio call has no propagated context
- **WHEN** a stdio request contains no supported trace context
- **THEN** the server starts a new request trace and does not add hidden fields to the tool's public input contract

#### Scenario: Malformed context is untrusted input
- **WHEN** incoming trace metadata is malformed
- **THEN** the server ignores or rejects that metadata according to the standard propagator, continues normal request validation, and does not convert the raw value into telemetry attributes

### Requirement: Stable outcomes and error telemetry
Telemetry SHALL classify request and tool outcomes using a bounded set that distinguishes success, validation error, authorization or policy denial, client cancellation, deadline exceeded, dependency error, tool error, protocol error, and internal error. Explicit cancellation SHALL not be reported as an internal server failure, and a valid empty result SHALL remain successful. Exceptions SHALL be recorded at the responsible span with a stable safe error type/code, while at most one operational error log is emitted at the request boundary. Classification SHALL survive translation to a client-facing MCP error through typed, bounded metadata and SHALL NOT depend on parsing an exception message. Exception messages and stack traces SHALL be sanitized or omitted when they can expose secrets, content, connection data, or managed paths.

#### Scenario: Client cancellation
- **WHEN** a caller cancels an in-flight tool operation
- **THEN** telemetry records a cancelled outcome and cancellation counter without classifying the operation as an internal error

#### Scenario: Validation failure
- **WHEN** a tool request fails schema or domain validation
- **THEN** telemetry records a bounded validation reason and stable error code without the invalid value or full request body

#### Scenario: Dependency failure is handled once
- **WHEN** a dependency call fails and the tool boundary returns an error
- **THEN** the dependency and tool spans show their respective outcomes and only the boundary emits the operational error log, avoiding duplicate exception logs

#### Scenario: Domain error is translated for the MCP client
- **WHEN** a domain validation or policy error is translated into a client-facing MCP error
- **THEN** telemetry receives its stable code, bounded outcome, and validation or denial reason from typed metadata without parsing the rendered client message

### Requirement: Low-cardinality metrics catalog
The server SHALL publish unsampled counters, histograms, and up/down counters for MCP request volume, request duration, in-flight requests, tool invocations, tool duration, validation failures, authorization or policy denials, protocol errors, timeouts, cancellations, dependency failures, suppressed logs, and telemetry drops. For each tool request and result, the server SHALL publish bounded non-content count metadata that is already available from parsed protocol structures. Exact input/output byte histograms SHALL be published only when original transport byte lengths are available without inspecting or reserializing payload content; otherwise the byte measurement SHALL be absent rather than estimated. Metric dimensions SHALL come from documented bounded sets such as method, configured tool name, status, protocol version, transport, dependency type, operation, signal, and reason. Trace IDs, span IDs, request IDs, user/agent/session/conversation IDs, resource URIs, paths, raw URLs, prompts, filenames, exception messages, and request-derived text SHALL NOT be metric labels.

#### Scenario: Successful tool metrics
- **WHEN** a configured tool completes successfully
- **THEN** request and tool counters increment, duration histograms record the operation, and the in-flight count returns to its prior value using only bounded labels

#### Scenario: Request-specific identifiers are present
- **WHEN** a call includes unique request, session, actor, operation, and memory identifiers
- **THEN** none of those values creates a metric time series or appears as a metric label

#### Scenario: Dependency and telemetry loss are measurable
- **WHEN** a dependency fails or telemetry is dropped or suppressed
- **THEN** the matching bounded failure or drop counter increments independently of trace sampling

#### Scenario: Exact transport byte length is unavailable
- **WHEN** a tool call exposes parsed argument and result structures but not their original transport byte lengths
- **THEN** bounded field or content-block counts are recorded, byte histograms are omitted, and telemetry does not reserialize or inspect payload values

#### Scenario: Protocol or policy request is rejected
- **WHEN** a request fails protocol handling or a tool attempt is denied by an existing authorization or policy control
- **THEN** the corresponding unsampled counter increments with only a documented bounded reason

### Requirement: Metadata-only privacy and security policy
Telemetry SHALL default to an allowlist of operational metadata and SHALL exclude prompts, hidden reasoning, tool arguments and results, memory/session content, evidence, credentials, tokens, cookies, connection strings, authorization data, full resource URIs, managed filesystem paths, arbitrary headers or query strings, and raw request, tool-call, client, actor, operation, memory, session, or conversation identifiers before formatting, SDK queueing, or export. Trace and span IDs MAY be emitted for signal correlation but SHALL NOT be metric labels. Content capture SHALL remain disabled unless a separately reviewed configuration explicitly enables classified fields with redaction, truncation, and retention controls. High-impact or destructive tool attempts SHALL emit a sampling-independent security/audit event containing time, configured tool, policy or operation class, outcome, trace ID, and a pseudonymous actor only when one is safely available.

#### Scenario: Synthetic secrets cross every telemetry path
- **WHEN** tool input, nested metadata, an exception, and a dependency response contain synthetic credentials, email addresses, bearer tokens, or private paths
- **THEN** none of those values appears in stderr logs, exported logs, spans, metrics, audit events, queues, or test snapshots

#### Scenario: Unknown input field is introduced
- **WHEN** a future request schema adds a field not present in the telemetry allowlist
- **THEN** that field is excluded from telemetry by default

#### Scenario: Destructive operation is attempted
- **WHEN** a destructive MCP tool succeeds, fails validation, is denied, or is cancelled
- **THEN** a bounded audit event records the operation class and outcome without raw actor, target, argument, or result content

### Requirement: Validated and versioned observability configuration
The server SHALL validate observability configuration at startup for enabled signals, exporter protocol and endpoint, timeout and queue bounds, sampling ratio, batch and flush limits, service identity, deployment environment, and telemetry size/cardinality limits. All signals SHALL carry a non-generic service name and the available service version, runtime version, environment, deterministic application schema and toolset versions, and pinned telemetry-schema and semantic-convention policy versions; logs and traces SHALL additionally carry per-process instance identity, while metrics SHALL omit it to avoid per-process time-series growth. Request-scoped signals SHALL carry transport and the negotiated MCP protocol version. Local development SHALL work without a collector, while an explicitly enabled invalid or insecure production exporter configuration SHALL fail startup with a safe actionable error. Disabling remote export SHALL preserve local activity creation, trace-correlated structured stderr diagnostics, and MCP tool behavior.

#### Scenario: Default local execution
- **WHEN** the server starts with the required repository identity and no collector endpoint
- **THEN** it serves MCP over stdio with structured stderr logging, local instrumentation, and no remote exporter dependency

#### Scenario: Invalid production endpoint
- **WHEN** remote export is enabled with a malformed endpoint, disallowed clear-text remote address, invalid sampling ratio, or unbounded queue/timeout value
- **THEN** startup fails before serving requests with a safe configuration error that contains no credentials

#### Scenario: Two negotiated protocol versions
- **WHEN** supported clients use different MCP protocol versions
- **THEN** logs and spans retain the actual negotiated version and metrics separate versions only through the bounded protocol-version label

### Requirement: Resilient vendor-neutral export and collection
The application SHALL export enabled signals asynchronously over OTLP to a collector without embedding a vendor-specific telemetry SDK in domain services. Export queues, retries, batches, shutdown flushes, and memory use SHALL be bounded; collector, network, or backend failure SHALL not fail, indefinitely delay, or exhaust memory on the MCP request path. The project SHALL provide a deployable collector baseline with OTLP receivers, early memory limiting, server-side sensitive-attribute removal, batching, authenticated/TLS-ready backend export, bounded sending queue, bounded exponential retry, and collector self-observability. Persistent buffering SHALL be documented as an explicit security and durability choice rather than silently enabled.

#### Scenario: Collector is unavailable
- **WHEN** the configured collector is unreachable during startup or request handling
- **THEN** MCP requests continue within documented latency bounds, memory remains bounded, and exporter failures or dropped telemetry become observable without recursive log amplification

#### Scenario: Server shuts down
- **WHEN** the MCP host receives a normal shutdown request
- **THEN** queued telemetry is flushed for no longer than the configured shutdown budget and the process exits even if export remains unavailable

#### Scenario: Collector queue approaches capacity
- **WHEN** backend export is slower than ingestion
- **THEN** collector queue utilization, refusal/drop, retry, memory, and exporter-failure telemetry is available for alerting

### Requirement: Sampling preserves operational truth
Metrics and required security/audit events SHALL remain independent of trace sampling. Trace sampling SHALL be configurable, preserve parent sampling decisions when valid context exists, and retain an operator-defined healthy baseline while supporting collector-side retention of errors, slow requests, canary versions, and selected high-value tools. Sampling decisions SHALL use only approved low-cardinality metadata and SHALL NOT depend on raw content.

#### Scenario: Successful traffic is sampled
- **WHEN** normal successful request volume exceeds the configured trace sampling ratio
- **THEN** only the selected traces are exported while all request and tool metrics remain complete

#### Scenario: Error-focused tail policy is enabled
- **WHEN** the collector receives successful and failed traces
- **THEN** its documented policy can retain failures and slow traces plus a bounded success baseline without inspecting raw arguments or results

### Requirement: Operational readiness assets and verification
The project SHALL document the signal/attribute catalog, version and stability policy, privacy and retention rules, exporter and collector deployment, dashboard queries, SLO/SLI definitions, sustained alert conditions, and runbooks for high tool latency, validation spikes, dependency outages, collector backlog/drops, and startup/export failures. Production readiness SHALL require automated verification of structured output, trace/log correlation, trace parentage, outcome mapping, redaction, attribute and label bounds, stdout purity, cancellation, collector outage, full queues, graceful shutdown, and telemetry overhead against a documented budget. Reports SHALL distinguish unit, integration, failure-injection, live collector/backend, load, and cross-platform evidence; skipped infrastructure checks SHALL remain unverified rather than pass.

#### Scenario: Release candidate is evaluated
- **WHEN** automated tests pass but live collector outage, load, or cross-platform checks were skipped
- **THEN** the readiness report lists those checks as unverified and does not claim full production readiness

#### Scenario: Dashboard and alert review
- **WHEN** operators review service, tool, protocol, dependency, security, and telemetry-pipeline dashboards
- **THEN** each view maps to documented low-cardinality signals, an SLI or diagnostic question, and a runbook-backed sustained alert where applicable

#### Scenario: Telemetry overhead exceeds budget
- **WHEN** representative load testing exceeds the documented CPU, allocation, latency, queue-memory, or bytes-per-request budget
- **THEN** the release is blocked or the budget is explicitly revised with evidence rather than silently accepting the regression
