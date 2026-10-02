# OnuObservability 0.1.0-alpha.8 release-readiness report - updated 2026-10-01

Status: **local migration candidate validated with explicit external/environment blockers**. This report does not authorize a Git push, external NuGet publication, signing, or stable promotion.

## Immutable candidate and rollback

- Candidate: `0.1.0-alpha.8` from the sibling `OnuObservability/artifacts/packages/` repository path.
- Exact artifact hashes and isolated-consumer receipt: [validated-packages.0.1.0-alpha.8.json](../../../../../OnuObservability/artifacts/consumer-locks/validated-packages.0.1.0-alpha.8.json).
- SBOM: [sbom.0.1.0-alpha.8.spdx.json](../../../../../OnuObservability/artifacts/supply-chain/sbom.0.1.0-alpha.8.spdx.json).
- Provenance: [provenance.0.1.0-alpha.8.json](../../../../../OnuObservability/artifacts/supply-chain/provenance.0.1.0-alpha.8.json).
- Rollback: restore AgentMemoryMCP's pre-migration observability implementation or select the last independently accepted immutable package version. `alpha.1` through `alpha.7` are retained as superseded local artifacts, not approved production releases.

## Linked release evidence

- Implementation and consumer parity: [implementation verification](implementation-verification-2026-09-29.md).
- External approvals and unresolved decisions: [external decisions](external-decisions.md).
- MCP API/schema comparison inputs: [telemetry baseline](baselines/mcp-telemetry.json) and [classification baseline](baselines/mcp-classification.json).
- Core public API/schema baselines: [core public API](../../../../../OnuObservability/tests/OnuObservability.Tests/Baselines/core-public-api.txt) and [core schema](../../../../../OnuObservability/tests/OnuObservability.Tests/Baselines/core-schema.txt).
- Package, dependency, vulnerability, license, and content policy: [artifact gate](../../../../../OnuObservability/eng/Test-PackageArtifacts.ps1), backed by the successful alpha.8 receipt above.
- Performance: [OnuObservability benchmark](../../../../../OnuObservability/artifacts/benchmarks/alpha8.json) and [AgentMemory consumer benchmark](../../../../.tmp/agent-alpha8-consumer-benchmark.json).

## Verification ledger

| Gate | Result | Evidence or blocker |
|---|---|---|
| OnuObservability Release build | Passed, 0 warnings/errors | Implementation verification report |
| OnuObservability tests | Passed: 133; failed: 0; skipped: 0 | Core 38, Hosting 47, ASP.NET Core 13, MCP 25, contract 4, integration 4, architecture 2 |
| Package and supply-chain policy | Passed | Exact-package receipt, SBOM, provenance, checksums, package artifact policy |
| Exact-package consumers | Passed | Web, Worker, and MCP isolated-feed consumers in the alpha.8 receipt |
| OnuObservability formatting | Passed | Full solution `dotnet format --verify-no-changes` |
| AgentMemory Release build | Passed, 0 warnings/errors | Implementation verification report |
| AgentMemory exact-candidate tests | Passed: 165; failed: 0; skipped: 3 | `.tmp/TestResults/agent-alpha8-full-live.trx`; both Collector/failure-injection tests passed and only live-Qdrant tests skipped |
| AgentMemory confirmation rerun | Passed: 163; failed: 0; skipped: 5 | 2026-10-01 local rerun; Docker was unavailable, so three Qdrant and two Collector tests skipped |
| Windows published MCP | Passed | `win-x64` single-file smoke for both supported protocols and all 26 tools |
| AgentMemory formatting | Partial | Changed telemetry files pass; repository-wide pre-existing whitespace violations remain |
| Documentation links | Passed | Scoped check of nine observability documentation files |
| Patch hygiene | Passed | `git diff --check` |
| OpenSpec structure | Passed | Strict validation |
| Live Collector and failure injection | Passed for exact alpha.8 candidate | Packaged Web live capture plus both AgentMemory Collector/failure-injection tests passed on 2026-09-30; Docker is unavailable for a repeat run on 2026-10-01 |
| Supported .NET 10 runtime matrix | Partial | Windows passed locally; Linux remains unverified because no matching local runner or approved emulation is available |
| Hosted CI | Unverified | The standalone repository contains Windows/Linux CI, but no hosted run has been observed |
| External publication/signing/stable promotion | Not approved | External decision record |

## Normative scenario mapping

| Spec scenario | Evidence or explicit blocker |
|---|---|
| Background service installs hosting support | Hosting registration tests and Worker packaged-consumer tests passed |
| Web application installs web support | ASP.NET Core registration tests and Web packaged-consumer tests passed |
| MCP server installs MCP support | MCP registration, contract, process, and packaged-consumer tests passed |
| Local host uses defaults | Configuration default tests passed |
| Standard environment setting overrides configuration | Configuration precedence tests passed |
| Unsafe endpoint is configured | Endpoint validation tests passed |
| Signals are exported from one process | In-memory resource/export integration tests passed; exact-candidate live Collector remains blocked |
| Adapter schema evolves additively | Checked public API/schema baselines and compatibility tests passed |
| Consumer submits synthetic sensitive data | Shared sensitive-data corpus tests passed across all local capture points |
| Unregistered metadata is supplied | Schema registry and safe-value rejection tests passed |
| Bounded value exceeds its limit | Normalization, fallback, truncation, and cardinality tests passed |
| Approved event is written during an activity | Safe logging correlation tests passed |
| Unapproved category writes a record | Logging allowlist/filter tests passed |
| Repeated event exceeds its rate | Deterministic bounded rate-limiter tests passed |
| Successful web request | ASP.NET Core middleware and Web process tests passed |
| Sensitive web request fails | Web privacy/error tests passed |
| Framework instrumentation is already active | Duplicate-span protection tests passed |
| Scheduled pass succeeds | Background operation runner and Worker tests passed |
| Host is shutting down | Typed host-stop and shutdown tests passed |
| Work is partially deferred | Aggregate partial/deferred Worker tests passed |
| Registered MCP tool completes | MCP filter/process tests and published protocol smokes passed |
| MCP domain failure is translated | Typed outcome-channel and `isError` conversion tests passed |
| Stdio server emits telemetry | Stdio safety tests and both published protocol smokes passed |
| Collector is unavailable | In-memory/exporter-isolation tests and exact alpha.8 live outage/recovery passed; application work continued through the failure profile |
| Healthy host shuts down | Shutdown tests and published MCP smokes passed |
| Exporter hangs during shutdown | Non-cooperative exporter shutdown tests passed |
| Root trace is not sampled | Sampling/unsampled-metric tests passed |
| High-cardinality inputs are exercised | High-volume bounded-cardinality tests passed |
| Application registers a bounded operation | Core operation/schema registration tests and AgentMemory descriptors passed |
| Test replaces time and exporter health ports | Deterministic TimeProvider and exporter-health composition tests passed |
| Consumer attempts to use an SDK-specific internal type | Architecture and public API boundary tests passed |
| Public API changes incompatibly | Compatibility baseline negative-fixture checks passed |
| Package is tested before promotion | Exact alpha.8 package policy and isolated-feed consumers passed; stable promotion not approved |
| Release evidence is incomplete | This report keeps blocked/unverified gates open rather than declaring release complete |
| AgentMemoryMCP migrates to the package | Exact alpha.8 references, source removal, build, tests, benchmark, and Windows publish smoke passed |
| Parity check fails | Exact alpha.8 local, live Collector/failure-injection, and performance parity passed; no unresolved mismatch remains and tasks 10.5/10.6 are complete |
| Consumer rolls back | Immutable earlier artifacts and pre-migration source history provide a documented rollback path; no stable package was promoted |

## Promotion decision

Do not promote to stable yet. The remaining mandatory gates are clean-checkout reproducibility, Linux .NET 10 support-matrix execution, hosted CI evidence, resolution of repository-wide formatting debt or an approved scope exception, and explicit signing/publication/stable-promotion approval.
