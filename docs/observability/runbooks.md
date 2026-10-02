# Observability runbooks

Use only bounded dimensions from the [signal catalog](configuration-and-signals.md). Never print environment variables, exporter headers, raw telemetry payloads, canonical memory, request arguments/results, paths, URLs, or exception text during diagnosis.

## High tool latency

- Symptom: sustained p95/p99 `mcp.tool.duration` increase with adequate volume.
- Safe queries: group by configured tool/status and compare request latency, inflight, dependency span duration, and dependency-error rate.
- Bounded checks: `Check-AgentMemoryObservability.ps1`; `memory_status`; CPU/RSS and queue utilization. Do not enable payload capture.
- Mitigation: reduce nonessential load, restore the affected dependency, or disable remote export if collector pressure correlates. Do not bypass transaction/approval rules.
- Recovery: latency returns below the approved threshold for two alert windows; error mix and inflight normalize.
- Escalation: tool owner, then storage/semantic dependency owner; attach only bounded charts and trace IDs.

## Validation spike

- Symptom: sustained increase in `mcp.validation.failures` by configured tool/registered reason.
- Safe queries: compare protocol version, deployed application schema/toolset versions, reason, and tool.
- Bounded checks: reproduce with synthetic non-sensitive input; compare `tools/list` schemas. Do not log rejected arguments.
- Mitigation: roll back an incompatible client/server release or correct the caller. Never relax validation merely to suppress telemetry.
- Recovery: ratio returns to baseline and representative valid/invalid contract tests pass.
- Escalation: MCP contract owner and client owner; security owner for policy/approval reasons.

## Dependency outage

- Symptom: `mcp.dependency.errors` or dependency spans show bounded `dependency_error`/deadline outcomes.
- Safe queries: dependency type/operation, status, duration, service version, environment.
- Bounded checks: repository dependency check scripts and `memory_status`; no credential echo or response-body dump.
- Mitigation: preserve lexical/local behavior, restore Ollama/Qdrant/filesystem availability, or reduce concurrency. Canonical files remain authoritative.
- Recovery: health checks succeed, error rate clears, pending work drains, and a representative MCP call succeeds.
- Escalation: dependency/platform owner; storage owner if canonical validation fails.

## Collector backlog or drops

- Symptom: queue utilization rises, receiver refusals, enqueue failures, send failures, or memory-limiter refusal.
- Safe queries: collector queue size/capacity, accepted/refused/sent/failed rates, RSS; app `mcp.telemetry.dropped` by signal/reason.
- Bounded checks: `Check-AgentMemoryObservability.ps1`; collector health and bounded recent error counts. Do not dump the queue or storage files.
- Mitigation: restore backend/network, scale collector within approved limits, temporarily reduce trace sampling, or safely disable remote export. Persistent mode requires prior approval.
- Recovery: queue drains to zero, sent rate resumes, enqueue/refusal counters stop increasing, RSS returns to baseline.
- Escalation: observability platform owner; security/incident owner if persistent telemetry may be exposed.

## Export or startup failure

- Symptom: safe startup validation error, collector unhealthy, or direct stderr event `1090 mcp.telemetry.dropped`.
- Safe queries: effective non-secret endpoint scheme/host class, protocol, enabled signals, health/self-metrics, configuration validation command.
- Bounded checks: run the check script and pinned collector `validate`; never display authorization/header values.
- Mitigation: correct validated configuration; otherwise set `Observability__ExporterEnabled=false` or `OTEL_SDK_DISABLED=true` and restart. MCP stdio and safe stderr remain operational.
- Recovery: startup succeeds, a synthetic call is accepted by the collector, no new exporter failures occur, and stdout stays JSON-RPC-only.
- Escalation: deployment owner, then observability platform owner.

## Safe remote-export disablement

- Symptom: backend/collector threatens request latency, memory, privacy, or incident containment.
- Safe action: set `Observability__ExporterEnabled=false` (or `OTEL_SDK_DISABLED=true`) through approved deployment configuration and restart the MCP process; stop the collector separately if required.
- Verify: MCP initialize, success and validation-failure calls work; stderr is bounded JSON; no new remote accepted items; local activities and safe logs remain.
- Recovery: re-enable only after endpoint/TLS/auth, privacy, queue, load, and retention checks pass.
- Escalation: production owner approval is required before restoring export after a privacy/security incident.
