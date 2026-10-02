# AgentMemoryMCP observability operations

AgentMemoryMCP consumes `OnuObservability.Hosting` and `OnuObservability.Mcp` from the approved sibling-repository feed. Reusable configuration, signal, privacy, adapter, and collector behavior is owned by the [OnuObservability documentation](../../../OnuObservability/docs/README.md). This directory documents only AgentMemory service overrides, deployment choices, dashboards/SLOs, runbooks, and migration evidence.

The checked-in `observability/*.yaml`, Compose overlays, and `scripts/*AgentMemoryObservability.ps1` files are versioned consumer deployment copies, not a second owner of reusable package behavior. Their classification and source package version are recorded in [`observability/asset-ownership.json`](../../observability/asset-ownership.json). CI runs `Test-AgentMemoryObservabilityAssetOwnership.ps1` so an undeclared file, package-version mismatch, or drift in the one package-copied capture profile fails explicitly. Service-owned profiles and scripts may diverge only for AgentMemory deployment names, paths, verification, and operational policy.

## Local disposable collector

Docker and the Compose plugin are prerequisites; the scripts install nothing.

```powershell
./scripts/Start-AgentMemoryObservability.ps1 -Mode Local
./scripts/Check-AgentMemoryObservability.ps1 -Mode Local
$env:Observability__ExporterEnabled = 'true'
$env:Observability__Endpoint = 'http://127.0.0.1:4318'
$env:Observability__ExporterProtocol = 'http/protobuf'
$env:Observability__ExportLogs = 'true'
# Start the MCP process normally.
./scripts/Stop-AgentMemoryObservability.ps1 -Mode Local
```

The default local sink is container tmpfs: it uses no credentials and is removed with the container. `readiness` is deliberately `collector_started_not_production_approved` or `operational_not_production_approved`.

## Profiles

| Profile | Configuration | Durability | Intended use |
|---|---|---|---|
| Local | `collector.local.yaml` | tmpfs only | local smoke and receiver checks |
| Test capture | local plus `docker-compose.test.yml` | explicit temporary bind mount | opt-in integration assertions only |
| Production | `collector.production.yaml` | bounded memory queue | reviewed remote TLS backend |
| Persistent | `collector.persistent.yaml` plus persistent overlay | named volume and fsync | explicit durability/security approval only |
| Failure test | `collector.failure-test.yaml` | bounded four-batch queue | automated slow/full/recovery injection only |

For production or persistent mode, provide `OTEL_BACKEND_ENDPOINT` as HTTPS and `OTEL_BACKEND_AUTHORIZATION` through the process environment or an approved secret manager. Environment variables are visible to sufficiently privileged host/container operators; do not put them in source, shell history, screenshots, or CI artifacts.

## Canonical references

- [OnuObservability package documentation](../../../OnuObservability/docs/README.md)
- [Package configuration](../../../OnuObservability/docs/configuration.md)
- [Package signals](../../../OnuObservability/docs/signals.md)
- [Package MCP adapter](../../../OnuObservability/docs/adapters.md)
- [AgentMemory compatibility and application schema](configuration-and-signals.md)
- [Dashboards, SLIs, SLO examples, and alerts](dashboards-and-slos.md)
- [Runbooks](runbooks.md)
- [Privacy and production approval checklist](privacy-and-production-checklist.md)
- [Verification evidence](verification.md)

Rollback is immediate and application-safe: set `Observability__ExporterEnabled=false` (or `OTEL_SDK_DISABLED=true`) and restart the MCP process. Structured safe stderr logging and local activities remain; remote export stops. Collector rollback is `Stop-AgentMemoryObservability.ps1`; persistent volumes are retained until an approved secure-deletion action.
