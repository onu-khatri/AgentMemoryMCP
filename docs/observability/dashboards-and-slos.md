# Dashboards, SLIs, SLO examples, and alerts

All thresholds below are provisional examples, not production commitments. Operators must approve traffic windows, exclusions, backend query syntax, thresholds, and paging routes. Validation errors, authorization denials, and caller cancellations are excluded from availability/reliability failure numerators and shown separately as product/security quality signals.

## Backend-neutral dashboard specification

| View/panel | Signal and aggregation | Bounded dimensions | Question / link |
|---|---|---|---|
| Service request rate | rate of `mcp.server.requests` | environment, method, protocol, status | traffic and outcome mix; link traces by service/time |
| Availability | success + documented client/tool errors divided by eligible requests | environment, protocol | is the service serving eligible requests? |
| Request latency | p50/p95/p99 `mcp.server.request.duration` | environment, method, status | user-visible latency; exemplars/trace link |
| Inflight | max `mcp.server.inflight` | environment, method | saturation or stuck requests |
| Tool reliability | rate of `mcp.tool.invocations` by status | environment, configured tool | which bounded tool fails? trace/log link |
| Tool latency | p50/p95/p99 `mcp.tool.duration` | environment, configured tool, status | slow tools and regressions |
| Validation quality | rate of `mcp.validation.failures` | configured tool, registered reason | caller/schema quality, not availability |
| Authorization/security | rate of `mcp.authorization.denials` and event 1101 | configured tool, registered reason/outcome | denied/destructive attempts; audit log link |
| Dependency health | rate of `mcp.dependency.errors` plus dependency spans | registered type/operation/status | Ollama, Qdrant, filesystem, maintenance |
| Protocol quality | rate of `mcp.protocol.errors`, timeouts, cancellations | registered method/protocol | client compatibility and deadline behavior |
| Log suppression | rate of `mcp.logs.suppressed` | registered event/tool/status | whether event rate limits hide detail |
| App telemetry loss | rate of `mcp.telemetry.dropped` | signal, registered reason | policy/export/flush failures; stderr link |
| Collector ingress | receiver accepted/refused item rates | receiver, transport, signal | is ingestion healthy? |
| Collector export | sent/failed/enqueue-failed item rates | exporter, signal | is backend delivery healthy? |
| Queue | queue size / queue capacity | exporter, signal | backlog/full risk |
| Collector memory | process RSS and memory-limiter refusal | collector instance | bounded-memory risk |
| Persistent storage | initializer/mount check plus queue/storage errors | deployment only | is explicit durable queue usable? |

Never group by trace/span ID, raw identifiers, URLs, paths, exception messages, arguments, results, or arbitrary caller values.

## SLIs and provisional objectives

| SLI | Formula | Provisional example |
|---|---|---|
| Availability | eligible requests without `dependency_error`, `deadline_exceeded`, `protocol_error`, or `internal_error` / eligible requests | 99.9% over 28 days |
| Tool reliability | successful + documented tool errors / eligible tool invocations; exclude validation, denial, caller cancellation | 99.5% over 28 days per critical tool |
| Request latency | eligible request duration | 99% under 1 s over 1 hour |
| Tool latency | eligible tool duration | 95% under 2.5 s by tool over 1 hour |
| Validation quality | validation failures / tool attempts | trend only until product baseline exists |
| Dependency reliability | dependency-error-free operations / dependency operations | 99.5% over 28 days by dependency |
| Telemetry delivery | sent items / accepted items, accounting for intentional sampling | 99% over 1 hour; never a service-availability SLI |

## Sustained alerts

| Alert example | Sustained condition | Runbook |
|---|---|---|
| Availability burn | both 5-minute fast burn and 1-hour slow burn exceed approved budget | [Dependency outage](runbooks.md#dependency-outage) or service owner |
| Tool latency | p95 over approved threshold for 15 minutes with minimum volume | [High tool latency](runbooks.md#high-tool-latency) |
| Validation spike | validation ratio exceeds baseline for 15 minutes | [Validation spike](runbooks.md#validation-spike) |
| Dependency errors | dependency error rate above threshold for 10 minutes | [Dependency outage](runbooks.md#dependency-outage) |
| Collector backlog | queue utilization above 80% for 10 minutes or enqueue failures nonzero | [Collector backlog or drops](runbooks.md#collector-backlog-or-drops) |
| Export failure | send failures for 10 minutes and sent rate zero | [Export or startup failure](runbooks.md#export-or-startup-failure) |

Do not page on a single validation error, denial, caller cancellation, exporter retry, or audit attempt. Production thresholds remain blocked until approved and recorded in the production checklist.
