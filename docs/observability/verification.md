# AgentMemory observability verification evidence

Evidence date: 2026-10-01. Local environment: Windows, .NET SDK 10.0.401/runtime 10.0.12. Docker Desktop/Engine was unavailable for the current `0.1.0-alpha.9` confirmation, so current live Collector checks are reported as unverified rather than passed.

AgentMemoryMCP consumes immutable local packages `OnuObservability.Hosting` and `OnuObservability.Mcp` `0.1.0-alpha.9` from the standalone sibling repository at `D:\RND\ONU\OnuObservability`. The reusable outbound HTTP registration and sanitizer are package-owned; AgentMemory retains only its reviewed loopback `/api/tags` and `/api/embed` request predicate. Restore assets resolve both references as packages and contain no OnuObservability project reference.

## Current commands

```powershell
dotnet restore ../OnuObservability/OnuObservability.sln --force-evaluate
dotnet build ../OnuObservability/OnuObservability.sln -c Release --no-restore -p:PackageVersion=0.1.0-alpha.9
../OnuObservability/eng/Pack-Local.ps1 -PackageVersion 0.1.0-alpha.9 -NoBuild
../OnuObservability/eng/Test-PackageArtifacts.ps1 -PackageVersion 0.1.0-alpha.9 -AllowUnapprovedMetadata
../OnuObservability/eng/Test-LocalPackages.ps1 -PackageVersion 0.1.0-alpha.9
./scripts/Test-AgentMemoryObservabilityAssetOwnership.ps1
dotnet restore AgentMemoryMCP.slnx --force-evaluate
dotnet test ../OnuObservability/OnuObservability.sln -c Release --no-build --no-restore
dotnet test AgentMemoryMCP.slnx -c Release --no-build --no-restore
openspec validate extract-onu-observability --strict
git diff --check
git diff --cached --check
```

## Verified for alpha.9

| Layer | Result |
|---|---|
| Release compile | OnuObservability production/test projects and AgentSession.MCP compiled successfully with 0 warnings and 0 errors in the focused builds. |
| Repository boundary | `OnuObservability` is an independent sibling Git repository on `main`; AgentMemory contains no nested source copy or OnuObservability project reference, and the package repository has no project reference back to AgentMemory. |
| Package suite | 134 passed, 0 failed, 0 skipped: Core 38, Hosting 48, ASP.NET Core 13, MCP 25, architecture 2, contract 4, integration 4. |
| AgentMemory suite | 168 total: 163 passed, 5 skipped, 0 failed. The skips are three opt-in live Qdrant tests and two opt-in live Collector tests. Authoritative result: `AgentSession.MCP.Tests/TestResults/agent-alpha9-full-authoritative.trx`. |
| HTTP ownership focus | 14/14 AgentMemory schema, Ollama HTTP-parentage/privacy, and consumer-parity tests passed. Hosting's complete 48/48 suite includes the package-owned reviewed-request and sanitizer test. |
| Package artifacts | Four `.nupkg` and four `.snupkg` files were created once and normalized; `checksums.0.1.0-alpha.9.json` records their hashes. Package content/dependency/vulnerability seeded gates passed with the explicit local-metadata override. |
| Exact-package consumers | Web, Worker, and MCP samples restored from the isolated filesystem feed, built without project references or unrelated adapters, and passed their package-consumer integration gate. Receipt: `../OnuObservability/artifacts/consumer-locks/validated-packages.0.1.0-alpha.9.json`. |
| Consumer package graph | `OnuObservability.Hosting/0.1.0-alpha.9` and `OnuObservability.Mcp/0.1.0-alpha.9` resolve as packages; AgentSession has zero project references and no direct `OpenTelemetry.Instrumentation.Http` reference. |
| Deployment-asset ownership | Twelve retained AgentMemory files are declared against alpha.9: eleven service-owned deployment copies and one hash-pinned package copy. The ownership/drift gate passed and is configured in CI. |
| Architecture and cleanup | Compiled architecture tests passed 2/2. Repository searches found no duplicate local provider/filter/pipeline implementation or stale direct HTTP instrumentation registration. |
| OpenSpec and diff hygiene | `openspec validate extract-onu-observability --strict`, scoped `git diff --check`, and staged `git diff --cached --check` passed. |

The first sandboxed AgentMemory full-suite attempt could not overwrite two generated schema fixtures and reported 161 passed, 5 skipped, and 2 filesystem-access failures. The authoritative rerun with workspace write access passed 163 with the same five environment-gated skips; the sandbox attempt is not treated as a product failure.

## Retained alpha.8 live evidence

The immutable alpha.8 candidate remains the last candidate exercised with Docker on 2026-09-30. Its packaged Web live capture and both AgentMemory Collector happy-path/failure-injection tests passed, and the full AgentMemory result was 165 passed, 3 skipped, 0 failed. This historical evidence supports behavioral continuity but does not replace an alpha.9 live Collector rerun.

## Explicitly unverified or pending

- Alpha.9 live Collector capture, slow/full backend failure, recovery, and shutdown checks because Docker is currently unavailable.
- Alpha.9 Windows and Linux published-binary protocol smokes; retained alpha.8 evidence is historical only.
- Hosted Windows/Linux CI results for the current candidate.
- `win-arm64`, `osx-arm64`, `linux-arm64`, and `linux-musl-x64` runtime smoke where no matching runner or approved emulation evidence exists.
- Alpha.9 performance/benchmark confirmation and refreshed SBOM/provenance evidence.
- Real production backend authentication, RBAC, region, retention/deletion, encryption, credential rotation, load/soak behavior, SLOs, alert routing, sampling, collector sizing, and persistent-storage approval.
- External publication, signing, and stable promotion, which remain unapproved.

Passing local automation establishes implementation evidence, not production readiness. Complete the [production approval checklist](privacy-and-production-checklist.md) and the remaining `extract-onu-observability` tasks before promotion.
