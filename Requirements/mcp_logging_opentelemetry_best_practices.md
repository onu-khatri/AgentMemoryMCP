# MCP Server Logging & OpenTelemetry Best Practices

> **Scope:** Production-grade observability guidance for Model Context Protocol (MCP) servers, with emphasis on logging, OpenTelemetry traces/metrics/logs, privacy, security, performance, and the MCP `2026-07-28` stateless protocol model.
>
> **Status note:** MCP and OpenTelemetry semantic conventions continue to evolve. Pin the MCP protocol version and OpenTelemetry semantic-convention version you implement, test upgrades, and avoid assuming experimental attributes are stable.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Core Observability Principles](#2-core-observability-principles)
3. [MCP Protocol and Transport Considerations](#3-mcp-protocol-and-transport-considerations)
4. [Logging Best Practices](#4-logging-best-practices)
5. [Distributed Tracing Best Practices](#5-distributed-tracing-best-practices)
6. [Context Propagation](#6-context-propagation)
7. [Span Design for MCP](#7-span-design-for-mcp)
8. [Metrics Best Practices](#8-metrics-best-practices)
9. [Recommended MCP Metrics](#9-recommended-mcp-metrics)
10. [OpenTelemetry Resource Attributes](#10-opentelemetry-resource-attributes)
11. [Recommended MCP Attributes](#11-recommended-mcp-attributes)
12. [Error and Exception Telemetry](#12-error-and-exception-telemetry)
13. [Privacy, PII, Secrets, and Data Governance](#13-privacy-pii-secrets-and-data-governance)
14. [Agent, Model, and Tool Observability](#14-agent-model-and-tool-observability)
15. [Schema Validation Observability](#15-schema-validation-observability)
16. [Authorization and Security Telemetry](#16-authorization-and-security-telemetry)
17. [OpenTelemetry Collector Architecture](#17-opentelemetry-collector-architecture)
18. [Collector Reliability and Resilience](#18-collector-reliability-and-resilience)
19. [Sampling Strategy](#19-sampling-strategy)
20. [Cardinality Management](#20-cardinality-management)
21. [Performance and Overhead](#21-performance-and-overhead)
22. [SLOs, SLIs, and Alerting](#22-slos-slis-and-alerting)
23. [Dashboards](#23-dashboards)
24. [stdio-Specific Guidance](#24-stdio-specific-guidance)
25. [Streamable HTTP Guidance](#25-streamable-http-guidance)
26. [Backward Compatibility](#26-backward-compatibility)
27. [Testing Observability](#27-testing-observability)
28. [Operational Runbooks](#28-operational-runbooks)
29. [Anti-Patterns](#29-anti-patterns)
30. [Example Structured Log](#30-example-structured-log)
31. [Example Trace Model](#31-example-trace-model)
32. [Example Metrics Catalog](#32-example-metrics-catalog)
33. [Example Collector Configuration](#33-example-collector-configuration)
34. [Production Readiness Checklist](#34-production-readiness-checklist)
35. [References](#35-references)

---

# 1. Executive Summary

Observability for an MCP server should answer five classes of questions:

1. **Protocol health** — Did the MCP request parse, validate, authenticate, execute, and return correctly?
2. **Tool behavior** — Which tool/resource/prompt was invoked, with what safe metadata, for how long, and with what result classification?
3. **Agent interaction** — Which agent/workflow initiated the action, and how does the MCP operation connect to the surrounding GenAI trace?
4. **Dependency health** — Which downstream database, API, filesystem, queue, model, or service caused latency or failure?
5. **Security and governance** — Was sensitive data exposed, access denied, a policy triggered, or an anomalous invocation attempted?

The most important design principles are:

- Keep **protocol traffic separate from application telemetry**.
- Treat **stdio stdout as protocol-only**.
- Prefer **structured logs**, not free-form text.
- Correlate logs with **`trace_id` and `span_id`**.
- Create a trace span for each meaningful MCP operation.
- Preserve W3C trace context where the transport permits it.
- Use explicit safe metadata for custom transports and stdio.
- Avoid raw prompt, tool input, output, document, credential, or PII capture by default.
- Use OpenTelemetry semantic conventions where applicable.
- Keep custom MCP attributes low-cardinality and versioned.
- Export through an **OpenTelemetry Collector** rather than coupling the server to a vendor.
- Use collector queues, retries, memory protection, and optionally persistent buffering.
- Never allow telemetry failure to break the MCP request path.
- Measure validation errors, authorization failures, tool latency, cancellations, dependency failures, and telemetry drops.
- Build observability around the modern MCP stateless request model rather than server affinity.

---

# 2. Core Observability Principles

## 2.1 Observability must never corrupt MCP protocol traffic

Telemetry must not alter, interleave with, or invalidate JSON-RPC messages.

For stdio transports:

- `stdout` is reserved for MCP protocol messages.
- Send normal application logs to `stderr`.
- Prefer an OTel SDK/OTLP exporter or log collector for structured telemetry.
- Never use debug `print()` statements to stdout.
- Ensure libraries used by the server do not unexpectedly print banners, warnings, stack traces, progress bars, or debug output to stdout.

For HTTP:

- Do not include internal logs in HTTP responses.
- Do not expose stack traces in MCP JSON-RPC errors.
- Keep telemetry asynchronous where possible.

## 2.2 Observability should be protocol-version aware

Record the negotiated MCP protocol version.

Behavior differs materially across protocol revisions. A modern `2026-07-28` server uses a stateless protocol core, while servers supporting older versions may retain session-oriented behavior.

Recommended attribute:

```text
mcp.protocol.version = "2026-07-28"
```

Do not combine telemetry from materially different protocol versions without retaining the version as a dimension.

## 2.3 Prefer explicit instrumentation over accidental logging

Instrument intentional events:

- request accepted
- schema validated
- auth decision
- tool execution start/end
- dependency calls
- cancellation
- timeout
- result classification
- serialization failure
- export failure

Avoid relying on arbitrary framework logs as the primary source of operational truth.

## 2.4 Telemetry should degrade gracefully

A backend outage must not stop the MCP server from serving requests.

Telemetry pipelines should:

- buffer within bounded limits
- retry with backoff
- drop telemetry when necessary rather than exhaust server memory
- expose their own drop/error counters
- avoid synchronous vendor requests in the request path

## 2.5 Metadata first, content opt-in

Collect metadata by default:

- tool name
- operation
- duration
- status
- payload byte size
- argument count
- result count
- schema version
- error type
- dependency target category

Treat actual content as opt-in because it may contain:

- prompts
- credentials
- API keys
- user identifiers
- private documents
- proprietary source code
- medical information
- financial data
- regulated PII

---

# 3. MCP Protocol and Transport Considerations

## 3.1 Modern MCP is stateless at the protocol core

The MCP `2026-07-28` specification introduced a stateless protocol core.

Implications:

- Do not depend on server affinity for telemetry correlation.
- Do not assume sequential requests reach the same process.
- Do not store essential trace context only in local process memory.
- Propagate correlation context with each request when possible.
- Use durable identifiers only if they have an application-level meaning.
- Distinguish protocol request correlation from user conversation identity.

## 3.2 Protocol-level sessions are not the observability primitive

Older MCP deployments may expose `Mcp-Session-Id`; the modern stateless protocol should not be designed around it.

Avoid:

```text
trace_id = hash(Mcp-Session-Id)
```

A trace identifies an execution path, not a long-lived logical session.

If you need a conversation or workflow identifier, use a separate application-level attribute such as:

```text
gen_ai.conversation.id
gen_ai.workflow.name
app.workflow.id
```

subject to privacy and cardinality policy.

## 3.3 Record MCP request method

Examples:

```text
initialize                  # legacy revisions
server/discover             # modern protocol where applicable
tools/list
tools/call
resources/list
resources/read
prompts/list
prompts/get
subscriptions/listen
tasks/get
tasks/update
```

A stable low-cardinality request method is useful for metrics and tracing.

## 3.4 Record logical target separately from method

For `tools/call`:

```text
mcp.method.name = "tools/call"
mcp.tool.name = "search_documents"
```

For `resources/read`:

```text
mcp.method.name = "resources/read"
mcp.resource.scheme = "file"
```

Be cautious recording entire resource URIs because they may contain:

- usernames
- tenant IDs
- paths
- query parameters
- credentials
- document names
- high-cardinality identifiers

## 3.5 Cancellation

Instrument cancellations distinctly from errors.

Recommended classifications:

```text
success
client_cancelled
deadline_exceeded
validation_error
authorization_denied
tool_error
dependency_error
protocol_error
internal_error
```

A user cancellation should not generally be treated as an internal server failure.

---

# 4. Logging Best Practices

## 4.1 Use structured logs

Prefer JSON or native OpenTelemetry LogRecords.

Recommended fields:

```text
timestamp
severity
body / message
service.name
service.version
deployment.environment.name
trace_id
span_id
mcp.protocol.version
mcp.method.name
mcp.tool.name
mcp.request.id
mcp.request.status
error.type
duration_ms
```

Only add fields when they are useful for debugging or operations.

## 4.2 Correlate every request-scoped log

Logs produced within an active trace should include:

```text
trace_id
span_id
trace_flags
```

For non-OTLP JSON logs, use the OpenTelemetry-recommended names:

```json
{
  "trace_id": "...",
  "span_id": "...",
  "trace_flags": "01"
}
```

## 4.3 Use event-oriented messages

Good:

```text
"Tool execution completed"
"Tool input validation failed"
"Authorization denied"
"Dependency request timed out"
```

Less useful:

```text
"Something went wrong"
"Calling tool"
"Error!"
```

## 4.4 Keep event names stable

Log bodies can be human-readable, but stable event fields are better for queries:

```text
event.name = "mcp.tool.completed"
event.category = "tool_execution"
```

## 4.5 Log levels

Suggested policy:

### TRACE / DEBUG
Use for local debugging, normally disabled in production.

Examples:

- lifecycle transitions
- safe schema metadata
- dependency retry decisions
- transport state transitions

Do not use debug mode as permission to log secrets.

### INFO
Normal significant events:

- server started
- configuration loaded
- tool execution completed
- graceful shutdown
- collector connection established

Avoid logging every low-value internal function call.

### WARN
Unexpected but recoverable conditions:

- dependency retry
- telemetry export backlog
- deprecated protocol version used
- near timeout
- invalid optional metadata
- partial result

### ERROR
A request or important operation failed:

- tool execution failure
- serialization failure
- auth subsystem unavailable
- required dependency failed

### FATAL / CRITICAL
Process cannot safely continue:

- unrecoverable startup configuration
- corrupt required key material
- server invariant failure

## 4.6 Avoid duplicate logs

Do not log the same exception at every layer.

Preferred approach:

- lower layer records span status / exception
- boundary layer emits one operational error log
- trace ties the context together

## 4.7 Use parameterized logging

Prefer:

```python
logger.info("Tool completed tool=%s status=%s", tool_name, status)
```

over preformatted string construction if your framework supports structured logging.

## 4.8 Use monotonic timers for duration

Wall clock can jump.

Measure elapsed durations using a monotonic clock.

## 4.9 Do not log entire request/response objects by default

Instead log:

```text
request_bytes
response_bytes
argument_count
content_item_count
result_count
has_text_content
has_binary_content
```

## 4.10 Add log rate limiting

Protect the system from:

- repeated invalid tool calls
- auth attack noise
- retry storms
- malformed client loops

Use suppression counters so operators still know logs were dropped:

```text
mcp.logs.suppressed
```

## 4.11 Sanitize before serialization

Redact data before it reaches:

- log formatter
- OTel SDK
- collector
- queue
- crash reporter

Do not rely only on backend-side redaction.

---

# 5. Distributed Tracing Best Practices

## 5.1 One request should have a clear server span

For each MCP request, create or continue a trace.

Conceptually:

```text
MCP request span
└── tool/resource/prompt operation span
    ├── database span
    ├── HTTP dependency span
    └── filesystem span
```

Depending on framework instrumentation, the outer HTTP/RPC span may already exist.

Avoid creating redundant duplicate spans that represent exactly the same operation.

## 5.2 Use OpenTelemetry SpanKind correctly

Typical choices:

- incoming MCP over HTTP: `SERVER`
- outgoing HTTP/database/service call: `CLIENT`
- internal tool execution: `INTERNAL`
- remote tool call modeled as an RPC: often `CLIENT`/`SERVER`

Do not mark every custom span as `SERVER`.

## 5.3 Record status correctly

Do not set span status to error for all non-success business outcomes.

Examples:

- schema validation failure: error
- uncaught tool exception: error
- timeout: error
- explicit cancellation: usually unset/error classification via attributes depending on policy
- valid "no results" response: not an error

## 5.4 Record exceptions

Use standard OTel exception recording so backend UIs can group failures.

Include:

```text
exception.type
exception.message
exception.stacktrace
```

Only include stack traces when appropriate and after checking that they do not expose sensitive values.

## 5.5 Trace downstream dependencies

Instrument:

- HTTP clients
- database drivers
- Redis
- object storage
- message queues
- vector databases
- filesystem access when meaningful
- subprocess calls
- model/provider APIs

The value of an MCP trace comes from knowing *why* the tool was slow, not merely that it was slow.

## 5.6 Avoid tracing trivial internal implementation details

Do not create spans for:

- every helper function
- every serialization step
- every property access

Spans should represent meaningful operations.

---

# 6. Context Propagation

## 6.1 Prefer W3C Trace Context

Use:

```text
traceparent
tracestate
```

where the transport allows normal propagation.

Use baggage sparingly.

## 6.2 Streamable HTTP

For HTTP deployments, propagate standard W3C headers through infrastructure:

- gateway
- load balancer
- reverse proxy
- MCP client
- MCP server
- downstream services

Verify headers are not stripped.

## 6.3 stdio

stdio does not provide HTTP headers.

Options include:

1. Start a fresh trace at the server boundary.
2. If both sides are under your control, carry trace context through a deliberately defined metadata mechanism.
3. Use application-level IDs for correlation when trace propagation is impossible.

Do **not** silently overload normal tool arguments with hidden trace data if it changes the public tool contract.

If using `_meta` or an extension mechanism, namespace it and document it.

Example concept:

```json
{
  "_meta": {
    "com.example.observability": {
      "traceparent": "00-..."
    }
  }
}
```

Only use such a mechanism when compatible with the client/server stack and protocol rules.

## 6.4 Do not propagate sensitive baggage

Never put these in W3C baggage:

- tokens
- emails
- raw user names
- prompts
- query text
- document content
- authorization headers

Baggage can spread widely across services.

## 6.5 Validate incoming trace context

Treat trace headers as untrusted input.

- use the OTel propagator implementation
- reject malformed values
- do not parse by ad hoc regex if the SDK already implements the standard
- prevent arbitrary user data from becoming trace attributes

---

# 7. Span Design for MCP

## 7.1 Suggested span hierarchy

Example:

```text
POST /mcp                         SERVER
└── tools/call search_documents  INTERNAL
    ├── db.query                  CLIENT
    └── object_store.get          CLIENT
```

In an agent-level trace:

```text
invoke_agent
├── chat
├── execute_tool
│   └── MCP tools/call
│       └── search_documents
└── chat
```

## 7.2 Span naming

Names should be predictable and low-cardinality.

Good:

```text
mcp tools/call
mcp tool search_documents
mcp resources/read
```

Avoid:

```text
mcp tool search_documents query="customer 82917 refund"
```

Never put raw arguments in span names.

## 7.3 Tool execution span

Recommended conceptual attributes:

```text
gen_ai.operation.name = "execute_tool"
gen_ai.tool.name = "search_documents"
mcp.method.name = "tools/call"
mcp.protocol.version = "2026-07-28"
mcp.tool.status = "success"
```

Because GenAI/MCP semantic conventions are evolving, validate exact attribute names against the version of OpenTelemetry semantic conventions you deploy.

## 7.4 Request ID

JSON-RPC request ID is useful for debugging but can be high-cardinality.

Prefer it on logs/spans, not metrics.

```text
mcp.request.id = "1234"
```

Never use request ID as a metric label.

## 7.5 Tool input metadata

Safe candidates:

```text
mcp.tool.argument_count
mcp.tool.input_bytes
mcp.tool.schema.version
mcp.tool.validation.result
```

Dangerous by default:

```text
mcp.tool.arguments.raw
gen_ai.tool.call.arguments
```

Only record raw arguments with explicit opt-in, redaction, size limits, and data classification.

## 7.6 Tool output metadata

Safe candidates:

```text
mcp.tool.output_bytes
mcp.tool.result_count
mcp.tool.content_item_count
mcp.tool.is_error
```

Avoid raw result content by default.

---

# 8. Metrics Best Practices

Metrics should be:

- low-cardinality
- stable over time
- useful for SLOs and capacity planning
- independent of specific trace sampling decisions

Prefer counters and histograms over logging-based metrics.

## 8.1 Counter rules

Counters only increase.

Examples:

```text
mcp.server.requests
mcp.tool.invocations
mcp.tool.errors
mcp.validation.failures
mcp.auth.denials
```

## 8.2 Histograms

Use histograms for:

```text
request duration
tool duration
dependency duration
payload size
result size
queue delay
```

Choose boundaries appropriate to expected latency.

## 8.3 Gauges

Useful for current values:

```text
mcp.server.inflight
mcp.telemetry.queue.depth
mcp.tool.concurrent
```

Do not use gauges for cumulative totals.

---

# 9. Recommended MCP Metrics

Names below are suggested custom names, not necessarily official stable OTel semantic-convention metric names.

| Metric | Type | Suggested dimensions | Purpose |
|---|---|---|---|
| `mcp.server.requests` | Counter | `method`, `status` | Total MCP requests |
| `mcp.server.request.duration` | Histogram | `method`, `status` | End-to-end request latency |
| `mcp.server.inflight` | UpDownCounter/Gauge | `method` | Concurrent requests |
| `mcp.tool.invocations` | Counter | `tool_name`, `status` | Tool execution volume |
| `mcp.tool.duration` | Histogram | `tool_name`, `status` | Tool latency |
| `mcp.tool.input.size` | Histogram | `tool_name` | Input payload size |
| `mcp.tool.output.size` | Histogram | `tool_name` | Result payload size |
| `mcp.validation.failures` | Counter | `tool_name`, `reason` | Invalid model/tool arguments |
| `mcp.auth.denials` | Counter | `method`, `reason` | Access denial volume |
| `mcp.protocol.errors` | Counter | `error_type` | JSON-RPC / MCP protocol failures |
| `mcp.server.cancelled` | Counter | `method` | Client cancellations |
| `mcp.server.timeouts` | Counter | `method`, `tool_name` | Deadline failures |
| `mcp.dependency.errors` | Counter | `dependency_type`, `operation` | Downstream failures |
| `mcp.rate_limit.rejections` | Counter | `scope` | Rate-limit decisions |
| `mcp.telemetry.dropped` | Counter | `signal`, `reason` | Lost observability data |

## 9.1 Status dimension

Use a bounded enumeration.

Example:

```text
success
validation_error
auth_denied
timeout
cancelled
dependency_error
tool_error
internal_error
```

Do not use exception messages as metric labels.

## 9.2 Tool names

Tool names are usually bounded by server configuration and are often safe metric dimensions.

However, confirm the server does not create dynamic tool names per tenant, user, document, or request.

---

# 10. OpenTelemetry Resource Attributes

Every signal should carry consistent resource identity.

Recommended:

```text
service.name
service.namespace
service.version
service.instance.id
deployment.environment.name
cloud.provider
cloud.region
cloud.availability_zone
k8s.cluster.name
k8s.namespace.name
k8s.pod.name
container.id
host.name
process.runtime.name
process.runtime.version
```

Use what is actually relevant to your deployment.

## 10.1 `service.name` is mandatory operational hygiene

Set a meaningful value:

```text
service.name = "mcp-search-server"
```

Do not accept a generic default such as:

```text
unknown_service
```

## 10.2 Version everything

Useful version fields:

```text
service.version
mcp.protocol.version
app.toolset.version
app.schema.version
otel.semconv.version   # if you maintain such a deployment metadata field
```

This enables before/after incident comparison.

---

# 11. Recommended MCP Attributes

Treat this section as an internal schema proposal, not as a claim that every field is an official stable OTel semantic convention.

## 11.1 Request attributes

```text
mcp.protocol.version
mcp.transport
mcp.method.name
mcp.request.id
mcp.request.status
mcp.request.input_bytes
mcp.response.output_bytes
```

## 11.2 Tool attributes

```text
mcp.tool.name
mcp.tool.status
mcp.tool.input_bytes
mcp.tool.output_bytes
mcp.tool.validation.status
mcp.tool.result_count
mcp.tool.content_item_count
```

## 11.3 Resource attributes

```text
mcp.resource.scheme
mcp.resource.operation
```

Record full URI only if explicitly approved.

## 11.4 Client metadata

If available and safe:

```text
mcp.client.name
mcp.client.version
```

Avoid unbounded raw user-agent strings as metric dimensions.

## 11.5 Agent metadata

Where the MCP server is integrated with an agent ecosystem:

```text
gen_ai.agent.id
gen_ai.agent.name
gen_ai.workflow.name
gen_ai.conversation.id
```

Check current semantic-convention stability before adopting.

## 11.6 Identity

Prefer pseudonymous IDs:

```text
enduser.pseudo.id
tenant.id
```

Avoid:

```text
email
phone
full_name
```

unless required, approved, and governed.

Never put user IDs on high-volume metrics unless cardinality is strictly bounded.

---

# 12. Error and Exception Telemetry

## 12.1 Separate failure categories

Do not collapse all failures into "500".

Recommended categories:

```text
protocol
validation
authentication
authorization
rate_limit
timeout
cancellation
dependency
tool_logic
serialization
internal
```

## 12.2 Log safe error codes

Example:

```text
error.type = "ToolValidationError"
error.code = "MCP_TOOL_INPUT_INVALID"
```

Prefer stable error code fields over message parsing.

## 12.3 Avoid secret leakage in exceptions

Exceptions can accidentally include:

- SQL with user values
- URLs with tokens
- filesystem paths
- request bodies
- OAuth headers

Sanitize before exporting.

## 12.4 Record retry behavior

For retrying dependencies:

```text
retry.count
retry.reason
```

Do not create one error alert per retry if the final operation succeeds.

## 12.5 Distinguish handled vs unhandled

A handled dependency retry is operationally different from an uncaught server crash.

---

# 13. Privacy, PII, Secrets, and Data Governance

## 13.1 Default deny for content capture

By default, do not record:

- prompts
- chat transcripts
- tool arguments
- tool results
- file contents
- resource contents
- authorization headers
- cookies
- API keys
- access tokens
- refresh tokens
- database connection strings

## 13.2 Redaction must happen early

Redact before:

```text
logger -> SDK -> batch queue -> collector -> backend
```

The safest telemetry is data never emitted.

## 13.3 Use allowlists rather than blocklists

Better:

```text
safe_fields = ["limit", "operation", "format"]
```

than:

```text
remove ["password", "token"]
```

Unknown future secret fields will bypass a blocklist.

## 13.4 Hash only when hashing is actually safe

Hashing an email or username is still pseudonymous personal data and can often be reidentified.

Use keyed HMAC if you need stable privacy-preserving correlation.

## 13.5 Do not log authorization headers

Always remove:

```text
Authorization
Proxy-Authorization
Cookie
Set-Cookie
X-API-Key
```

and equivalent custom headers.

## 13.6 Query parameters

Strip or classify URL query strings before export.

## 13.7 Resource URI safety

Resource URIs can contain sensitive paths.

Prefer:

```text
scheme=file
resource_type=document
```

over:

```text
file:///home/alice/acquisition-secret/project-merger.pdf
```

## 13.8 Limit telemetry payload sizes

Set:

- max log body length
- max span attribute length
- max event count per span
- max attributes per signal
- truncation markers

Example:

```text
telemetry.truncated = true
```

## 13.9 Retention tiers

Consider separate retention:

- security logs: longer, controlled access
- traces: medium
- debug logs: short
- content-bearing GenAI telemetry: shortest / opt-in

## 13.10 Access control

Observability backends frequently become shadow data lakes.

Apply:

- RBAC
- audit logs
- tenant isolation
- encryption
- retention controls
- legal hold policy
- export restrictions

---

# 14. Agent, Model, and Tool Observability

An MCP server may not call an LLM itself, but its spans often sit inside an agent trace.

## 14.1 Preserve parent-child relationships

Ideal trace:

```text
agent workflow
└── LLM chat
    └── execute_tool
        └── MCP tools/call
            └── downstream dependency
```

## 14.2 Do not infer agent intent from private reasoning

Log the observable operation, not hidden chain-of-thought.

Safe concepts:

```text
requested tool
tool arguments metadata
workflow name
explicit user-visible request category
```

Avoid attempting to record or reconstruct hidden model reasoning.

## 14.3 GenAI semantic conventions

OpenTelemetry GenAI conventions include concepts such as:

```text
gen_ai.operation.name
gen_ai.agent.id
gen_ai.agent.name
gen_ai.workflow.name
gen_ai.tool.name
gen_ai.tool.call.id
gen_ai.tool.call.arguments
gen_ai.tool.call.result
```

Content-bearing attributes can contain sensitive data and should be opt-in.

## 14.4 Tool call identifiers

A tool-call ID is useful on traces/logs but not metrics.

Use for correlation between:

- model tool request
- MCP invocation
- result returned to agent

## 14.5 Token metrics

An MCP server should only report token usage if it actually has authoritative token data.

Do not estimate model tokens and present them as exact.

If the MCP tool invokes an LLM directly, use applicable GenAI semantic conventions.

---

# 15. Schema Validation Observability

Schema validation failures are especially important in agentic systems.

They indicate:

- model/client incompatibility
- ambiguous tool description
- tool schema drift
- outdated client cache
- hallucinated argument names
- invalid enum choices

## 15.1 Record validation failures

Recommended:

```text
mcp.validation.failures{tool_name, reason}
```

Bounded reasons:

```text
missing_required
wrong_type
unknown_field
invalid_enum
constraint_violation
schema_mismatch
invalid_json
```

## 15.2 Do not use raw invalid value as a metric label

Bad:

```text
reason="unknown customer id 829171993"
```

Good:

```text
reason="constraint_violation"
```

## 15.3 Safe validation logs

Example:

```json
{
  "event.name": "mcp.tool.validation_failed",
  "tool_name": "create_ticket",
  "reason": "missing_required",
  "field": "priority"
}
```

Only log field name if the schema itself is non-sensitive.

## 15.4 Monitor schema-version mismatch

If clients may cache tool definitions, expose:

```text
toolset.version
tool.schema.version
```

in diagnostics.

---

# 16. Authorization and Security Telemetry

## 16.1 Record security decisions, not secrets

Useful:

```text
auth.result
auth.scheme
auth.policy
auth.reason
```

Avoid:

```text
access_token
refresh_token
client_secret
raw_claims
```

## 16.2 Track denials

Metric:

```text
mcp.auth.denials
```

Dimensions:

```text
method
reason
```

Bounded reasons:

```text
missing_credentials
invalid_credentials
expired
insufficient_scope
policy_denied
tenant_mismatch
```

## 16.3 Audit sensitive tools

For high-impact tools, emit a dedicated audit event with:

- timestamp
- pseudonymous actor ID
- tool name
- authorization policy
- outcome
- trace ID
- target class, not necessarily target value

Examples:

- sending email
- modifying repositories
- deleting cloud resources
- executing shell commands
- modifying financial records

## 16.4 Detect unusual patterns

Possible security signals:

- repeated unknown tool names
- repeated schema failures
- high auth denial rate
- unusual tool invocation burst
- tool never normally used by a client type
- repeated access to restricted resources
- excessive payload size

Keep anomaly detection out of the synchronous MCP request path.

---

# 17. OpenTelemetry Collector Architecture

## 17.1 Decouple application instrumentation from vendor backends

Recommended architecture:

```text
MCP Server
   |
   | OTLP gRPC/HTTP
   v
OpenTelemetry Collector
   |
   +--> tracing backend
   +--> metrics backend
   +--> logs backend
```

Benefits:

- vendor neutrality
- centralized redaction
- batching
- retries
- routing
- sampling
- enrichment
- credential isolation
- exporter changes without application redeploy

## 17.2 Sidecar vs node agent vs gateway

### Sidecar
Pros:

- strong workload isolation
- short local network hop
- app-specific processing

Cons:

- more resource overhead
- more collector instances

### DaemonSet / node agent
Pros:

- efficient in Kubernetes
- fewer collectors

Cons:

- node-level blast radius

### Central gateway
Pros:

- centralized routing/sampling
- easier management

Cons:

- additional network dependency

Large production systems often use:

```text
application -> local/agent collector -> gateway collector -> backend
```

## 17.3 Use TLS and authentication

OTLP endpoints should not be publicly writable.

Use:

- TLS/mTLS
- private networking
- authenticated exporters
- secret rotation

---

# 18. Collector Reliability and Resilience

## 18.1 Memory limiter

Use the Collector memory limiter to prevent runaway memory use.

Best practice from the Collector project includes:

- configure memory limiting
- put the memory limiter early in the processor pipeline
- coordinate limits with container/host memory
- set `GOMEMLIMIT`
- leave room for traffic spikes

## 18.2 Sending queues

Enable exporter sending queues for remote backends.

Monitor:

```text
otelcol_exporter_queue_size
otelcol_exporter_queue_capacity
```

A full queue means telemetry loss is imminent or occurring.

## 18.3 Retry with exponential backoff

Use bounded retries.

Do not retry forever in unbounded memory.

## 18.4 Persistent queue

For telemetry where data loss matters, consider file-backed exporter queues.

Trade-offs:

- increased disk usage
- more operational complexity
- local sensitive telemetry persisted to disk

Encrypt and secure disk if required.

## 18.5 Backpressure

Understand what happens when collector processors refuse data.

Application exporters should not block MCP calls indefinitely.

## 18.6 Monitor the Collector itself

Collect collector self-observability:

- accepted records
- refused records
- dropped records
- exporter failures
- queue size
- processor errors
- CPU
- memory
- disk usage for persistence

Observability infrastructure must itself be observable.

---

# 19. Sampling Strategy

## 19.1 Metrics should generally be unsampled

Metrics are already aggregated.

## 19.2 Head sampling

Simple and cheap.

Example:

```text
sample 5% of successful requests
sample 100% of errors
```

But pure head sampling cannot know whether a request will later fail.

## 19.3 Tail sampling

Useful at a collector gateway to retain:

- errors
- high latency
- specific high-value tools
- security events
- rare protocol versions

Example policies:

```text
retain all error traces
retain all > 5s traces
retain 2% normal success traces
retain 100% canary-version traces
```

## 19.4 Do not make sampling decisions on high-cardinality content

Use safe low-cardinality fields available at span start where possible.

## 19.5 Keep security/audit events independently

Do not depend on trace sampling for required audit logs.

---

# 20. Cardinality Management

Cardinality mistakes can make an observability platform unusable or expensive.

## 20.1 Good metric labels

```text
tool_name
method
status
error_category
protocol_version
transport
environment
```

provided the values are bounded.

## 20.2 Bad metric labels

```text
trace_id
span_id
request_id
user_id
conversation_id
resource_uri
query
prompt
filename
exception_message
URL with path parameters
```

## 20.3 Normalize HTTP routes

Record:

```text
http.route = "/mcp"
```

not arbitrary URLs.

## 20.4 Cap dynamic dimensions

If supporting dynamic tool registration, consider:

- hashing to controlled buckets for some analytical use cases
- allowing only configured tool-name labels
- aggregating unknown tools under `other`

Do not silently create millions of time series.

---

# 21. Performance and Overhead

## 21.1 Instrumentation budget

Track telemetry overhead itself.

Targets depend on system, but operators should measure:

- CPU overhead
- allocation rate
- export latency
- queue memory
- bytes emitted per request

## 21.2 Batch exports

Prefer batch span/log processors in production.

Avoid one network export per log line.

## 21.3 Do not serialize large payloads only to discard them

If content capture is disabled, do not first JSON serialize entire request/output for telemetry sizing.

Use known byte lengths where possible.

## 21.4 Avoid expensive redaction regexes in hot paths

Prefer structural field-based redaction.

## 21.5 Use asynchronous exporters

But keep bounded queues.

## 21.6 Protect against telemetry amplification

One request should not create thousands of logs/spans under normal behavior.

Apply:

- span count limits
- event count limits
- recursion safeguards
- log rate limiting

---

# 22. SLOs, SLIs, and Alerting

## 22.1 Availability SLI

Example:

```text
successful valid MCP requests /
eligible MCP requests
```

Decide whether these count against availability:

- client validation failures
- auth denials
- cancellations

Usually, they should not be treated identically to server failures.

## 22.2 Tool reliability SLI

Per tool:

```text
successful tool executions /
valid authorized tool executions
```

## 22.3 Latency SLI

Track:

- p50
- p90
- p95
- p99

per high-value tool.

## 22.4 Validation quality SLI

Useful agent/tool-design signal:

```text
validation failures /
tool invocation attempts
```

An increase can indicate schema or description regression.

## 22.5 Dependency SLI

Track downstream failure and latency by dependency class.

## 22.6 Suggested alerts

Alert on sustained conditions, not single events:

- high tool error ratio
- p99 latency regression
- validation failures spike
- auth denials spike
- collector queue nearly full
- telemetry drop rate > threshold
- server restart/crash loop
- timeout ratio increase
- unusually high cancellation rate

---

# 23. Dashboards

A production dashboard should have multiple views.

## 23.1 Executive/service health

- request rate
- success ratio
- latency percentiles
- error ratio
- saturation

## 23.2 Tool view

Per tool:

- invocation count
- success/error
- latency
- input/output size
- validation failures
- timeout/cancel rate

## 23.3 Protocol view

- methods by version
- transport
- protocol errors
- deprecated client versions
- unsupported methods

## 23.4 Dependency view

- dependency latency
- failure rate
- retry rate
- timeout rate

## 23.5 Security view

- auth denials
- restricted tool usage
- suspicious failure bursts
- rate-limit events

## 23.6 Telemetry pipeline

- collector health
- export failures
- queue utilization
- dropped logs/spans
- backend ingestion errors

---

# 24. stdio-Specific Guidance

## 24.1 stdout is protocol-only

This is non-negotiable.

Bad:

```python
print("Starting MCP server...")
```

if it writes to stdout.

Use:

```python
print("Starting MCP server...", file=sys.stderr)
```

or a logger configured to stderr.

## 24.2 Configure child libraries

Some dependencies write to stdout.

Test production startup with protocol parsing enabled to detect pollution.

## 24.3 No ANSI UI output

Disable:

- progress bars
- spinners
- rich terminal banners
- interactive prompts
- colorized debug banners

on stdout.

## 24.4 Telemetry export

Prefer:

```text
MCP JSON-RPC -> stdout
logs -> stderr / OTLP
traces -> OTLP
metrics -> OTLP
```

## 24.5 Crash handling

If the process crashes:

- stack trace may go to stderr
- try to flush telemetry with a short bounded shutdown timeout
- never delay process death indefinitely

---

# 25. Streamable HTTP Guidance

## 25.1 Modern request model

For MCP `2026-07-28`, each Streamable HTTP message is carried as a request to the MCP endpoint; responses may be a JSON object or request-scoped SSE stream.

Instrument each POST independently.

## 25.2 Standard HTTP instrumentation

Use normal OTel HTTP server semantic conventions.

Typical signals include:

```text
http.request.method
http.route
http.response.status_code
server.address
network.*
```

Do not duplicate HTTP fields under custom MCP names unless necessary.

## 25.3 MCP-specific headers

Modern MCP defines standardized headers such as:

```text
MCP-Protocol-Version
Mcp-Method
Mcp-Name
```

Do not automatically dump all headers to telemetry.

If recording these, sanitize and normalize first.

## 25.4 `Mcp-Name`

Potentially useful for routing and observability, but resource URI values may be sensitive or high-cardinality.

Tool names are typically safer than raw resource URIs.

## 25.5 SSE/request-scoped streaming

For streamed responses, record:

- total duration
- time to first response chunk if useful
- stream completion status
- bytes/chunks
- cancellation

Do not emit one metric label per event.

---

# 26. Backward Compatibility

Servers may support clients using earlier MCP versions.

## 26.1 Tag protocol version

Always include negotiated protocol version in traces/logs.

## 26.2 Older sessions

If older revisions use session IDs:

- treat session ID as sensitive/high-cardinality
- do not use as a metric label
- do not assume it exists on modern protocol revisions
- do not make telemetry correctness dependent on it

## 26.3 MCP Logging feature

The MCP protocol-level Logging feature is deprecated in `2026-07-28`.

For new servers:

- use `stderr` for local stdio logging
- use OpenTelemetry for structured observability

Do not build a new production logging architecture around deprecated `logging/setLevel` / log notifications.

## 26.4 Legacy HTTP+SSE

Legacy HTTP+SSE has been deprecated in favor of Streamable HTTP.

Track usage if you maintain compatibility so you know when it is safe to remove.

---

# 27. Testing Observability

## 27.1 Unit tests

Test:

- redaction
- field allowlists
- metric label bounds
- status mapping
- error classification
- resource attributes

## 27.2 Golden tests for logs

Given a known request, verify the emitted structure.

Ensure:

- no secrets
- expected correlation IDs
- bounded fields
- correct status

## 27.3 Trace integration test

Run:

```text
test client -> MCP -> dependency
```

Verify all spans share the intended trace and parent relationships.

## 27.4 stdout pollution test

For stdio:

1. start the server
2. issue protocol messages
3. parse every stdout line as expected MCP framing
4. fail if arbitrary application output appears

## 27.5 Failure injection

Simulate:

- dependency timeout
- DNS failure
- backend 500
- auth provider outage
- collector outage
- full exporter queue
- cancellation
- invalid schema
- huge input
- malformed trace headers

## 27.6 Collector outage test

The server should continue serving MCP requests when the collector is unavailable.

## 27.7 PII regression test

Feed synthetic secrets:

```text
API_KEY_TEST_123
person@example.com
Bearer abc123
```

Assert they do not appear in exported telemetry.

---

# 28. Operational Runbooks

Create runbooks for common alerts.

## 28.1 Tool latency high

Check:

1. tool-level duration
2. dependency child spans
3. queueing/concurrency
4. payload size
5. retry count
6. recent release
7. downstream rate limits

## 28.2 Validation failures high

Check:

1. affected tool
2. field/reason distribution
3. tool schema change
4. client version
5. tool description change
6. stale client cache
7. model/provider change

## 28.3 Auth denials high

Check:

1. reason
2. affected client
3. token expiration
4. scope changes
5. authorization-server health
6. deployment configuration

## 28.4 Collector dropping data

Check:

1. queue capacity
2. backend availability
3. collector memory
4. disk if persistent queue used
5. exporter retries
6. rate of telemetry generation

---

# 29. Anti-Patterns

Avoid these patterns.

## 29.1 Logging to stdout on stdio MCP servers

Can corrupt the protocol.

## 29.2 Logging raw tool arguments globally

Creates privacy and security risk.

## 29.3 Logging raw tool results globally

May leak documents, credentials, proprietary data, or PII.

## 29.4 Request IDs as metric labels

Causes extreme cardinality.

## 29.5 One trace per conversation

Traces should represent bounded execution paths, not days-long user sessions.

## 29.6 Direct vendor SDK coupling everywhere

Makes migration, centralized governance, redaction, and routing harder.

## 29.7 Synchronous observability calls

Can turn telemetry outages into production outages.

## 29.8 Treating all validation failures as server incidents

They may indicate client/model/schema quality rather than server availability.

## 29.9 Sampling away every successful trace

You still need healthy baseline traces for comparison.

## 29.10 Keeping only logs

Logs alone make distributed agent/tool latency diagnosis harder.

## 29.11 Keeping only traces

Metrics are better for SLOs, alerts, and long-term aggregation.

## 29.12 High-cardinality tool metrics

Dynamic tenant-specific tool names can explode costs.

## 29.13 Stack traces returned to clients

Keep internal details in protected telemetry.

## 29.14 Assuming MCP session stickiness

Modern MCP is stateless at the protocol core.

## 29.15 Reconstructing hidden model reasoning

Observe actions and inputs/outputs under policy; do not attempt to store hidden chain-of-thought.

---

# 30. Example Structured Log

```json
{
  "timestamp": "2026-09-27T04:30:15.123Z",
  "severity": "INFO",
  "event.name": "mcp.tool.completed",
  "body": "Tool execution completed",
  "service.name": "mcp-search-server",
  "service.version": "2.7.1",
  "deployment.environment.name": "production",
  "trace_id": "4bf92f3577b34da6a3ce929d0e0e4736",
  "span_id": "00f067aa0ba902b7",
  "trace_flags": "01",
  "mcp.protocol.version": "2026-07-28",
  "mcp.transport": "streamable_http",
  "mcp.method.name": "tools/call",
  "mcp.tool.name": "search_documents",
  "mcp.request.status": "success",
  "duration_ms": 84.2,
  "mcp.tool.input_bytes": 312,
  "mcp.tool.output_bytes": 4821,
  "mcp.tool.result_count": 7
}
```

Notice what is *not* present:

- raw query text
- raw document content
- access token
- user email
- full resource URI
- arbitrary headers

---

# 31. Example Trace Model

```text
Trace: 4bf92f...

invoke_agent                                    INTERNAL
├── chat                                        CLIENT
├── execute_tool search_documents               INTERNAL
│   └── POST /mcp                               CLIENT
│       └── mcp tool search_documents           INTERNAL
│           ├── POST /vector-search             CLIENT
│           └── GET /object/{id}                 CLIENT
└── chat                                        CLIENT
```

Useful analysis:

- total agent time
- time spent in model
- time spent waiting for MCP
- time spent inside tool
- which dependency dominated
- whether retries occurred

---

# 32. Example Metrics Catalog

A concrete baseline catalog:

```text
mcp.server.requests
  type: counter
  labels:
    - method
    - status
    - protocol_version
    - transport

mcp.server.request.duration
  type: histogram
  unit: s
  labels:
    - method
    - status

mcp.server.inflight
  type: updowncounter
  labels:
    - method

mcp.tool.invocations
  type: counter
  labels:
    - tool_name
    - status

mcp.tool.duration
  type: histogram
  unit: s
  labels:
    - tool_name
    - status

mcp.validation.failures
  type: counter
  labels:
    - tool_name
    - reason

mcp.auth.denials
  type: counter
  labels:
    - method
    - reason

mcp.server.timeouts
  type: counter
  labels:
    - method
    - tool_name

mcp.server.cancelled
  type: counter
  labels:
    - method

mcp.dependency.errors
  type: counter
  labels:
    - dependency_type
    - operation

mcp.telemetry.dropped
  type: counter
  labels:
    - signal
    - reason
```

Review metric names against your organization's naming conventions before production rollout.

---

# 33. Example Collector Configuration

Illustrative configuration only; validate component names and options against the Collector release you deploy.

```yaml
receivers:
  otlp:
    protocols:
      grpc:
      http:

processors:
  memory_limiter:
    check_interval: 1s
    limit_mib: 512
    spike_limit_mib: 100

  attributes/redact:
    actions:
      - key: authorization
        action: delete
      - key: http.request.header.authorization
        action: delete
      - key: gen_ai.tool.call.arguments
        action: delete
      - key: gen_ai.tool.call.result
        action: delete

  batch:
    timeout: 5s
    send_batch_size: 1024

exporters:
  otlp:
    endpoint: observability-backend.example.internal:4317
    tls:
      insecure: false
    sending_queue:
      enabled: true
      queue_size: 5000
    retry_on_failure:
      enabled: true
      initial_interval: 5s
      max_interval: 30s
      max_elapsed_time: 10m

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [memory_limiter, attributes/redact, batch]
      exporters: [otlp]

    metrics:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [otlp]

    logs:
      receivers: [otlp]
      processors: [memory_limiter, attributes/redact, batch]
      exporters: [otlp]
```

For higher resilience, consider a persistent queue using Collector file storage where appropriate.

---

# 34. Production Readiness Checklist

## Protocol safety

- [ ] stdio `stdout` contains protocol traffic only.
- [ ] Application logs go to `stderr` or OTLP.
- [ ] No progress bars/banners/debug prints can reach stdout.
- [ ] MCP protocol version is recorded.
- [ ] Modern stateless behavior does not rely on session affinity.
- [ ] Older protocol compatibility is explicitly tested.

## Logging

- [ ] Logs are structured.
- [ ] `trace_id` and `span_id` are correlated.
- [ ] Event names are stable.
- [ ] Duplicate exception logging is minimized.
- [ ] Log rate limiting exists.
- [ ] Raw tool inputs/results are disabled by default.
- [ ] Logs have size limits.

## Tracing

- [ ] MCP request spans exist.
- [ ] Tool execution spans exist where useful.
- [ ] Downstream dependencies are traced.
- [ ] W3C context propagates over HTTP.
- [ ] stdio context strategy is documented.
- [ ] Span names are low-cardinality.
- [ ] Errors and exceptions follow OTel conventions.
- [ ] Cancellation is distinguishable from internal errors.

## Metrics

- [ ] Request counter.
- [ ] Request duration histogram.
- [ ] In-flight request metric.
- [ ] Tool invocation counter.
- [ ] Tool duration histogram.
- [ ] Validation failure counter.
- [ ] Auth denial counter.
- [ ] Timeout counter.
- [ ] Cancellation counter.
- [ ] Dependency error counter.
- [ ] Telemetry drop counter.
- [ ] Metric labels have bounded cardinality.

## Privacy/security

- [ ] Credentials are redacted before export.
- [ ] Authorization headers are never logged.
- [ ] PII fields are allowlisted.
- [ ] Resource URIs are classified/redacted.
- [ ] Query strings are sanitized.
- [ ] Tool content capture is explicit opt-in.
- [ ] Telemetry backend has RBAC.
- [ ] Retention is documented.
- [ ] Audit requirements are separately addressed.
- [ ] Synthetic secret leakage tests pass.

## Collector

- [ ] Applications export via OTLP/Collector where practical.
- [ ] Collector has memory limiting.
- [ ] Batch processing is enabled.
- [ ] Remote exporters use queues.
- [ ] Retry is configured.
- [ ] Persistent queue considered for important telemetry.
- [ ] Collector self-metrics are monitored.
- [ ] Queue utilization alerts exist.
- [ ] Exporter failure alerts exist.
- [ ] Collector credentials are rotated securely.

## Reliability

- [ ] Collector outage does not break MCP serving.
- [ ] Backend outage does not cause unbounded application memory.
- [ ] Graceful shutdown has a bounded telemetry flush.
- [ ] Telemetry load tests exist.
- [ ] Failure-injection tests exist.

## Operations

- [ ] Service health dashboard exists.
- [ ] Tool dashboard exists.
- [ ] Protocol/version dashboard exists.
- [ ] Dependency dashboard exists.
- [ ] Telemetry pipeline dashboard exists.
- [ ] SLOs are defined.
- [ ] Alert thresholds are tested.
- [ ] Runbooks exist for common alerts.

---

# 35. References

The following sources are useful starting points. Always confirm the version corresponding to your deployed MCP and OpenTelemetry releases.

## Model Context Protocol

- MCP `2026-07-28` specification release:
  https://blog.modelcontextprotocol.io/posts/2026-07-28/

- MCP `2026-07-28` changelog:
  https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/changelog.mdx

- MCP transport overview:
  https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/basic/transports/index.mdx

- MCP Streamable HTTP:
  https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/basic/transports/streamable-http.mdx

## OpenTelemetry

- OpenTelemetry logs specification:
  https://opentelemetry.io/docs/specs/otel/logs/

- Trace Context in non-OTLP log formats:
  https://opentelemetry.io/docs/specs/otel/compatibility/logging_trace_context/

- RPC semantic conventions:
  https://opentelemetry.io/docs/specs/semconv/rpc/

- JSON-RPC semantic conventions:
  https://opentelemetry.io/docs/specs/semconv/rpc/json-rpc/

- MCP semantic-convention registry entry:
  https://opentelemetry.io/docs/specs/semconv/registry/attributes/mcp/

- GenAI semantic-convention registry:
  https://opentelemetry.io/docs/specs/semconv/registry/attributes/gen-ai/

- OpenTelemetry Collector resiliency:
  https://github.com/open-telemetry/opentelemetry.io/blob/main/content/en/docs/collector/resiliency.md

- Collector memory limiter:
  https://github.com/open-telemetry/opentelemetry-collector/blob/main/processor/memorylimiterprocessor/README.md

---

## Final Guidance

A well-instrumented MCP server should make it possible to answer, quickly and safely:

> **Which agent or client invoked which MCP capability, what happened inside the server and its dependencies, how long did each stage take, what failed, and can we diagnose it without exposing the user's private data?**

If the telemetry design cannot answer that question—or answers it by dumping raw prompts, inputs, outputs, credentials, or documents—then the observability design is incomplete.
