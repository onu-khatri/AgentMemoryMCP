# OnuObservability implementation verification - updated 2026-10-01

This is a local prerelease and AgentMemoryMCP migration checkpoint. It is not approval for an external NuGet publication, Git push, or stable promotion.

## Exact package candidate

- Candidate: `0.1.0-alpha.8`; `alpha.1` through `alpha.7` remain immutable superseded local artifacts.
- `dotnet build OnuObservability.sln -c Release --no-restore -m:1 -nr:false`: passed with 0 warnings and 0 errors.
- OnuObservability tests: 133 passed, 0 failed, 0 skipped (38 core, 47 Hosting, 13 ASP.NET Core, 25 MCP, 4 contract, 4 integration, and 2 architecture).
- Four `.nupkg` and four `.snupkg` artifacts passed metadata, content, dependency, test-dependency, vulnerability, and checksum policy.
- Exact-package Web, Worker, and MCP isolated-feed consumers passed; receipt: `OnuObservability/artifacts/consumer-locks/validated-packages.0.1.0-alpha.8.json`.
- Exact SHA-256 subjects have SPDX 2.3 SBOM and SLSA-shaped provenance under `OnuObservability/artifacts/supply-chain/`.
- Package repository metadata points to `https://github.com/onu-khatri/OnuObservability.git`; Source Link remains disabled for local builds unless explicitly enabled and is enabled automatically in the approved GitHub repository.

## AgentMemoryMCP migration

- AgentSession consumes exact local packages `OnuObservability.Hosting` and `OnuObservability.Mcp` version `0.1.0-alpha.8`; restore assets contain no source-project reference to OnuObservability.
- Legacy `Observability:*` settings are translated to package settings without changing stdio transport, exporter defaults, bounded shutdown, or the 26-tool public contract.
- Seventeen AgentMemory-owned dependency/maintenance descriptors are registered through the package builder. Unknown dependency type/operation pairs fail closed to `other`.
- The package owns the `mcp.dependency.errors` instrument through `IMcpDependencyFailureRecorder`; AgentMemory no longer registers a duplicate `McpTelemetry` meter.
- The reusable local configuration, provider, exporter, MCP filters, tool registry, logging, outcome-context, resource, and shutdown implementations were removed (23 production files). Generic legacy test suites were replaced by package tests and retained executable consumer-parity tests.
- `dotnet build AgentMemoryMCP.slnx -c Release --no-restore -m:1 -nr:false`: passed with 0 warnings and 0 errors.
- Exact-candidate AgentSession tests after source removal: 165 passed, 0 failed, 3 skipped. Result: `.tmp/TestResults/agent-alpha8-full-live.trx`. Both live Collector/failure-injection tests passed; the three skips require live Qdrant.
- A 2026-10-01 confirmation run with Docker unavailable passed 163 tests with the same three Qdrant and two Collector tests skipped; this environment-limited rerun does not replace the retained exact-candidate live evidence.
- A self-contained, single-file `win-x64` publish passed the protocol smoke for both `2025-11-25` and `2026-07-28`: 26 unique tools, success and validation-failure calls, JSON-RPC-only stdout, bounded JSON stderr, and graceful shutdown.
- Consumer benchmark after migration passed all provisional budgets for disabled, local JSON, and batched OTLP scenarios. Report: `.tmp/agent-alpha8-consumer-benchmark.json`.
- Package schema compatibility tests compare the MCP manifest to the checked AgentMemory baseline. Executable tests verify stdout purity, bounded metadata-only stderr, exporter failure isolation, storage behavior, cancellation, shutdown, and tool contracts.
- Repository search finds no stale local filter/provider registration or duplicate local MCP meter implementation.
- `dotnet format ../OnuObservability/OnuObservability.sln --no-restore --verify-no-changes` passed. Targeted formatting verification passed for the changed AgentMemory telemetry classifiers; repository-wide AgentMemory formatting still reports unrelated pre-existing whitespace violations and is not claimed as clean.
- The scoped observability Markdown link check passed for nine AgentMemory documentation files, `git diff --check` passed, and `openspec validate extract-onu-observability --strict` passed.

## Defects found and corrected during migration

- Export-health fan-out originally emitted only `onu.telemetry.dropped`; the MCP observer now preserves the bounded `mcp.telemetry.dropped` compatibility metric and stderr event.
- Classified tool exceptions initially escaped instead of becoming MCP `isError` results; the package filter now performs typed conversion without message parsing.
- Safe JSON events now retain stable event names, levels, UTC timestamps, and trace flags.
- A storage-only test worker lacked the new package recorder dependency; it now supplies an explicit no-op recorder, while production registers the real package recorder first.
- Concurrent test collectors used non-thread-safe lists/snapshot copying; the test infrastructure now snapshots concurrent collections safely.

## Explicitly unverified or blocked

- Docker Desktop/Engine is not currently running (`docker_engine` named pipe absent), so live checks could not be repeated on 2026-10-01. Exact `alpha.8` evidence is retained from 2026-09-30: the packaged Web collector capture completed and both AgentMemory Collector/failure-injection tests passed.
- The approved runtime baseline is .NET 10 only. Windows .NET 10 validation passed locally; the standalone repository CI declares Windows/Linux .NET 10 coverage, but no hosted run has been observed and Linux remains unverified.
- Live Qdrant tests remain environment-gated and were skipped.
- No external Git repository push, external NuGet publication, package signing, or stable promotion occurred. Those actions remain unapproved.

The scenario-by-scenario evidence and release-gate disposition are recorded in `release-readiness-2026-09-30.md`.
