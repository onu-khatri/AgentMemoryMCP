# AgentMemoryMCP extraction baseline

Evidence date: 2026-09-28. Source revision: `0876a297e6d85a66dba4898e209b74e445960460` plus the current staged production-observability implementation. The checkout was intentionally not reset or cleaned because the staged implementation belongs to the user. Generated build, publish, collector-capture, and benchmark scratch output is under ignored `.tmp/extract-onu-observability/`.

Environment:

- Windows `10.0.26200`, x64
- .NET SDK `10.0.401`, MSBuild `18.9.11`, runtime `10.0.12`
- Docker Engine `29.7.2`, Compose `v5.3.1`
- pinned Collector Contrib `0.161.0@sha256:fd328de2552466ad78385e1b1289c3f2402b1c45f265b252aab1955b42845ac1`
- pinned Linux smoke runtime `mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled@sha256:18d4848091a40d13dbfdd6a8340c1657dc3e2f2d7fa2f042e9d162e68669dbc9`

## Commands and results

| Check | Command/profile | Fresh result |
|---|---|---|
| Restore | `dotnet restore AgentMemoryMCP.slnx --force --no-cache --force-evaluate` | passed; 4 projects restored |
| Release build | `dotnet build AgentMemoryMCP.slnx -c Release --no-restore` | passed; 0 warnings, 0 errors |
| Complete tests | `dotnet test AgentMemoryMCP.slnx -c Release --no-build` | 230 total: 225 passed, 5 skipped, 0 failed; 2m 17.52s wall clock, test runner reported 2.2920 minutes |
| Skipped tests | complete test run | 3 opt-in live Qdrant tests and 2 opt-in collector tests |
| Windows x64 publish | self-contained Release, single-file project settings, isolated output | passed |
| Windows x64 smoke | `Test-PublishedMcp.ps1`, protocols `2025-11-25` and `2026-07-28` | 2/2 passed; 26 tools, success/failure calls, JSON-RPC-only stdout, bounded JSON stderr, graceful shutdown |
| Linux x64 publish | self-contained Release, single-file project settings, isolated output | passed |
| Linux x64 smoke | same harness through the pinned `runtime-deps` container, both protocols | 2/2 passed with the same assertions |
| Compose rendering | local, persistent, and test-capture profiles | 3/3 passed |
| Collector configuration | local plus production/persistent/failure profiles with non-secret placeholder endpoints | 4/4 passed |
| Collector slow/full queue and recovery | opt-in `SlowFullBackendDoesNotBlockMcpAndQueuedExportRecovers` | 1/1 passed in 11.8409s |
| Collector all-signal file capture | opt-in `LocalCollectorReceivesCorrelatedTracesMetricsAndLogsWithoutBreakingMcp` | 0/1 passed; failed in 20.1446s |
| Benchmark smoke | `Measure-AgentMemoryObservability.ps1 -Iterations 50` | 3/3 scenarios passed provisional budgets |
| Extraction baseline validator | `evidence/Validate-Baseline.ps1` | passed: 22 sources, 77 unique mapped paths, 25 options, 9 outcomes, 26 tools, 17 metrics, 5 events |

## Live collector limitation

The all-signal file-capture test timed out waiting for its deployment marker after stopping the collector. The capture contained one correlated `mcp.tool.completed` log record, but not the trace and metric batches. The stopped pinned Collector exited with code `2` after a Go nil-pointer panic in `fileexporter.(*fileWriter).export` while the batch metrics pipeline was flushing during shutdown.

This is recorded as a baseline failure, not a package regression and not a passing check. The generic collector test profile must be corrected or the pinned collector advanced with evidence before package release gates can claim a passing all-signal live capture. The separate slow/full-queue failure and recovery test passed, and all four collector configurations plus all Compose renderings validated.

## Benchmark profile

The retained machine-readable result is [benchmark-baseline.json](baselines/benchmark-baseline.json).

| Scenario | p50 / p95 / p99 ms | req/s | CPU ms/request | allocated B/request | queue B | emitted B/request | Budget |
|---|---:|---:|---:|---:|---:|---:|---|
| instrumentation disabled | 0.0002 / 0.0014 / 0.0069 | 773993.81 | 0 | 131.04 | 0 | 0 | passed |
| local JSON | 0.0536 / 0.2032 / 0.4479 | 13471.28 | 0.3125 | 6252.64 | 314488 | 1288.78 | passed |
| OTLP batch | 0.0515 / 0.2446 / 0.8232 | 11950.57 | 3.125 | 74263.04 | 354672 | 2395.42 | passed |

These are local smoke measurements, not production capacity claims. The budgets remain provisional and require workload/operator approval.

## Explicitly unverified

- .NET 8 consumption does not exist yet; it belongs to the new package repository matrix.
- `win-arm64`, `osx-arm64`, `linux-arm64`, and `linux-musl-x64` runtime smoke were not run locally.
- The three opt-in live Qdrant tests were not run.
- Real backend authentication/RBAC, retention, encryption at rest, signing, provenance, feed publication, hosted CI, production load, soak, and stable promotion remain unverified or unapproved.
- The live all-signal collector capture is failing as described above.
