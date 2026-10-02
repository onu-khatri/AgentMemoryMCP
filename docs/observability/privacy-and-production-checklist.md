# Telemetry privacy and production approval checklist

## Classification and prohibited content

Operational metadata in the catalog is internal telemetry. Credentials, tokens, cookies, connection strings, full URLs/query strings, managed paths, prompts, hidden reasoning, tool arguments/results, memory/session/evidence content, arbitrary headers, exception messages/stacks, and raw client/actor/operation/memory/session/conversation identifiers are prohibited before formatting, SDK queueing, or export. Content capture is disabled and requires a separate reviewed design, classification, redaction, truncation, access, retention, and incident plan.

Trace IDs are correlation data, not identity or metric dimensions. Destructive-operation events are bounded operational audit hints; they are not a complete authorization audit trail and contain a pseudonymous actor only if a separately approved safe mapping exists.

## Backend and storage controls

- Apply least-privilege RBAC separately for telemetry administrators, readers, incident responders, and application operators.
- Encrypt remote transport with TLS and backend storage at rest; rotate credentials through an approved secret manager.
- Define retention and deletion by signal/classification; do not inherit vendor defaults silently.
- Treat persistent collector queues as telemetry storage: restrict volume access, encrypt host storage where required, set bounded size, document crash recovery, and use approved secure deletion.
- Document data-subject/record-location limitations: the application intentionally excludes raw personal/session content, but incident correlation IDs may still be regulated metadata.
- During an incident, preserve bounded metadata and access logs; never attach raw queue files, environment dumps, or secrets to tickets.

## Required production approvals

All boxes require named operator approval and evidence; automation passing alone is insufficient.

- [ ] Backend owner, tenant, endpoints, regional/data-residency placement, and access roles approved.
- [ ] TLS trust, authentication mechanism, secret injection/rotation, and non-disclosure in logs/artifacts approved.
- [ ] Signal retention, deletion, legal/incident hold, encryption, and backup policy approved.
- [ ] Dashboard access, trace/log linking, and audit logging approved.
- [ ] Application head sampling and collector tail-sampling percentages/capacity approved.
- [ ] Availability/reliability/latency SLOs, exclusions, alert thresholds, paging routes, and maintenance windows approved.
- [ ] Persistent queue need, volume size, host encryption, access, recovery, retention, and secure deletion approved—or persistent mode disabled.
- [ ] Load/overhead budget and representative workload approved.
- [ ] Live backend, outage, recovery, and rollback exercises completed in the target environment.
- [ ] Supported RID/platform evidence reviewed; untested RIDs remain explicitly unverified.
- [ ] Data-subject request and telemetry security-incident handling owners identified.

## Incident boundaries

If prohibited content is suspected, disable remote export, preserve access evidence without copying payloads, restrict backend/volume access, notify security/privacy owners, follow the approved deletion/hold decision, rotate exposed credentials, and verify sanitizer/application policy before re-enabling. Do not use MCP tools to grant backend access or authorize deletion.
