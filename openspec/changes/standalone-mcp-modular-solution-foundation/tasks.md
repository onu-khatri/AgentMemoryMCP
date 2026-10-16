## 1. Capture compatibility and ownership baselines

- [x] 1.1 Run the current restore, Release build, test suite, package metadata inspection, and MCP process smoke checks before moving files; record exact pass/fail evidence so migration regressions are distinguishable from existing failures.
- [x] 1.2 Capture the existing MCP discovery surface—tool names, descriptions, annotations, input/output schemas, and structured result shapes—and verify a machine-readable baseline is available to ContractTests.
- [x] 1.3 Capture canonical storage paths, representative repository/session/memory fixtures, cursor/checkpoint/restart behavior, and package ID/command metadata; verify the fixtures can be read by the current executable without Ollama or Qdrant.
- [x] 1.4 Classify every production source file and external package into Host, Protocol, Core, Session, AgentMemory, shared Infrastructure, or Service Defaults ownership; verify there are no unassigned files and document every current cross-owner dependency cycle that requires a seam.

## 2. Establish the multi-project build graph

- [x] 2.1 Add `global.json`, `Directory.Build.props`, and `Directory.Packages.props` using the existing .NET 10 SDK and resolved package versions without upgrades; verify deterministic restore succeeds with central package management enabled.
- [x] 2.2 Create buildable projects under `src/` for Host, StdioHost, Protocol, Core, Infrastructure, ServiceDefaults, Session.Contracts, Session, AgentMemory.Contracts, and AgentMemory; verify each boundary is a distinct `.csproj`, not only a folder or namespace.
- [x] 2.3 Create UnitTests, IntegrationTests, ContractTests, ArchitectureTests, and EndToEndTests projects under `tests/`, relocate the test worker project, and relocate benchmarks under `benchmarks/`; verify all projects build as empty or minimally scaffolded targets before behavior moves.
- [x] 2.4 Rebuild `AgentMemoryMCP.slnx` with architectural solution folders and only the approved projects; verify `dotnet sln AgentMemoryMCP.slnx list` matches the design inventory.
- [x] 2.5 Add initial ArchitectureTests that parse the real `.slnx`, `.csproj`, package, and assembly references against the allowed matrix; verify an intentionally invalid fixture/reference is rejected and the scaffold graph passes.

## 3. Extract the stable Core and module contracts

- [x] 3.1 Move platform-neutral request context, correlation, client/scope/repository resource descriptors, and security requirements into Core; verify Core compiles without ASP.NET Core, MCP SDK, OnuObservability hosting/MCP, Qdrant, Ollama, or filesystem implementation packages.
- [x] 3.2 Move normalized error/result contracts, cancellation-aware execution metadata, bounds, retry/idempotency eligibility policies, and only meaningful replaceable abstractions into Core; verify focused UnitTests cover validation, error categories, and policy decisions.
- [x] 3.3 Define Session.Contracts for lifecycle, append-to-session, task coordination, resume, checkpoint, requests, responses, and registration descriptors; verify public contracts preserve the baseline schemas and contain no MCP attributes or infrastructure types.
- [x] 3.4 Define AgentMemory.Contracts for reusable memory operations, status, curation, recall, indexing, requests, responses, and registration descriptors; verify public contracts preserve the baseline schemas and contain no Session implementation, MCP attribute, Qdrant, or Ollama types.
- [x] 3.5 Resolve each recorded dependency cycle through the smallest Core or module-contract seam and remove temporary compatibility references as each cycle closes; verify no generic shared project, speculative interface, or direct Session-to-AgentMemory implementation reference is introduced.

## 4. Extract shared Infrastructure without changing storage

- [x] 4.1 Move platform filesystem access, safe path resolution, canonical root/repository scoping, atomic file operations, managed transactions, and shared lock primitives into Infrastructure implementations of Core abstractions; verify existing path traversal, atomicity, transaction recovery, and repository-isolation integration tests pass against unchanged fixtures.
- [x] 4.2 Move shared resilience and dependency-health primitives into Infrastructure while keeping capability-specific retry decisions in modules; verify failure-isolation tests prove one unavailable integration does not destabilize unrelated module operations.
- [x] 4.3 Move shared observability adapters and OnuObservability composition helpers to Infrastructure/Protocol/Host boundaries; verify Core has no observability package reference and telemetry tests preserve normalized outcomes, redaction, and bounded dimensions.
- [x] 4.4 Move shared validated storage/configuration Options to their owning boundary and enable fail-fast validation; verify invalid root, repository identity, timeout, and provider settings prevent startup without exposing secrets.

## 5. Extract the Session vertical module

- [x] 5.1 Move session activation/listing, session-specific `append_agent_memory`, task coordination, resume snapshot/cursor, and checkpoint implementations into the Session project; verify Session references only Core and Session.Contracts and preserves the existing public behavior.
- [x] 5.2 Keep Session-specific canonical persistence and snapshot construction with the Session module while consuming shared filesystem abstractions; verify pagination, cursor mismatch/expiry, unknown values, checkpoints, restart continuity, actor/repository isolation, and cancellation tests pass without Ollama or Qdrant.
- [x] 5.3 Add one explicit Session registration extension and ownership descriptor covering tools, Core abstractions, external dependencies, authorization, failures, observability, and tests; verify Host composition can register Session without service-locator access or AgentMemory implementation references.
- [x] 5.4 Move focused Session behavior tests to UnitTests/IntegrationTests and add negative architecture coverage for concrete AgentMemory coupling; verify the Session test slice and ArchitectureTests pass independently.

## 6. Extract the AgentMemory vertical module

- [x] 6.1 Move reusable `memory_*` orchestration, canonical memory records, status, curation, cleanup, compaction, outcomes/events, review, reindex, and migration behavior into AgentMemory; verify AgentMemory references only Core and AgentMemory.Contracts for platform/module collaboration.
- [x] 6.2 Keep Qdrant and Ollama adapters, typed clients, projection, vector coordination, and provider-specific resilience inside AgentMemory because they are module-specific; verify provider tests cover outage degradation, cancellation, timeout, migration, and recovery without leaking provider types into Core or Protocol.
- [x] 6.3 Add one explicit AgentMemory registration extension and ownership descriptor covering tools, external dependencies, authorization, failures, observability, and tests; verify Host composition can register AgentMemory without Session implementation references.
- [x] 6.4 Move focused memory behavior and provider tests to UnitTests/IntegrationTests and preserve optional real-provider test semantics; verify deterministic tests pass offline and environment-dependent tests report skipped/unverified rather than false success.

## 7. Extract the MCP Protocol boundary

- [x] 7.1 Move MCP attributes, tool adapters, protocol contracts/mapping, filters, and stable protocol error mapping into Protocol, grouped by Session and AgentMemory surface; verify Protocol references Core plus module contract projects only and contains no persistence/provider implementation code.
- [x] 7.2 Replace broad or scanning-based registration with explicit tool/resource/prompt registration and preserve the saved capability inventory; verify ContractTests detect duplicate, missing, renamed, or newly exposed capabilities.
- [x] 7.3 Implement protocol validation for required fields, lengths, collection/page/response bounds, cancellation, and safe error mapping without duplicating module rules; verify malformed, oversized, cancelled, timeout, dependency, resync, and unexpected scenarios return stable categories without raw paths or exceptions.
- [x] 7.4 Add centralized authorization filters and capability-visibility behavior based on platform-neutral request/security contracts; verify unauthenticated, unknown-client, insufficient-scope, repository-boundary, and authorized discovery/invocation scenarios.

## 8. Establish independent hosts and operational composition

- [x] 8.1 Create ServiceDefaults for OpenTelemetry, health checks, resilience defaults, and service metadata without references to Core capability logic or module implementations; verify both hosts can consume it and the project remains independently buildable.
- [x] 8.2 Create the ASP.NET Core Host with Streamable HTTP, stateless-by-default handling, explicit module/tool registration, validated configuration, authentication/authorization, rate limiting, health/readiness, and graceful shutdown; verify the host starts independently and maps the configured MCP endpoint.
- [x] 8.3 Create provider-neutral local/CI bearer-token configuration and first-class request context containing client, optional user/tenant, scopes, repository resource, and correlation data; verify policy and resource tests cover least-privilege access without selecting a production identity provider.
- [x] 8.4 Refactor the existing executable/package into StdioHost using the same Protocol, Core, module, Infrastructure, and ServiceDefaults registrations; verify package ID, command metadata, startup/shutdown behavior, and baseline stdio MCP contracts remain compatible.
- [x] 8.5 Verify both hosts propagate cancellation through Protocol, contracts, modules, filesystem, HTTP, and provider operations and normalize failures consistently; verify cancellation/error telemetry and client-visible outcomes through focused integration tests.

## 9. Complete purpose-specific verification

- [ ] 9.1 Populate UnitTests with Core policy, module behavior, validation, mapping, and normalized error tests; verify the project runs without network, containers, Qdrant, or Ollama.
- [ ] 9.2 Populate IntegrationTests with canonical filesystem, configuration, authentication, module adapter, restart, concurrency, and provider-degradation tests using disposable infrastructure; verify environmental dependencies are explicit and isolated.
- [ ] 9.3 Populate ContractTests with independent-client HTTP and stdio discovery/invocation checks against the saved baseline; verify names, descriptions, annotations, schemas, authorization, validation, errors, cancellation, and structured outputs remain compatible.
- [ ] 9.4 Complete ArchitectureTests for the allowed project/package graph, Core independence, Protocol restrictions, Host-only composition, module isolation, explicit registrations, consumer isolation, and prohibited generic shared projects; verify every rule has a failing negative fixture and passes against production assemblies.
- [ ] 9.5 Populate EndToEndTests with container/independent-process health, readiness, authorized existing-tool flow, restart continuity, graceful shutdown, telemetry export, and supported shared-storage replica scenarios; record unavailable Docker/provider evidence as unverified.
- [ ] 9.6 Update the relocated TestWorker and benchmarks to reference only the target assemblies they exercise; verify worker scenarios and benchmark compilation do not reintroduce a dependency on a monolithic compatibility assembly.

## 10. Deployment, CI, documentation, and final gates

- [ ] 10.1 Add a multi-stage Dockerfile, Compose configuration, environment examples, canonical storage mount, non-secret local auth settings, health/readiness probes, and protocol-safe logging; verify image build and independent container startup.
- [ ] 10.2 Update CI to run deterministic restore, format check, Release build, UnitTests, ArchitectureTests, IntegrationTests, ContractTests, security/dependency scanning, container build, and applicable EndToEndTests; verify required jobs fail on a seeded architecture or contract regression.
- [ ] 10.3 Update root/module/deployment documentation with the final project map, allowed dependency matrix, module ownership template, registration flow, configuration, local HTTP/stdio commands, storage assumptions, and future-module guidance; verify all paths, links, and commands from the repository root.
- [ ] 10.4 Run package vulnerability/dependency checks, secret/redaction checks, and least-privilege authorization tests; verify findings are resolved or explicitly recorded before delivery.
- [ ] 10.5 Run the complete clean restore, formatting, Release build, all test projects, package build, container checks, and contract-baseline comparison; record each result separately as passed, failed, skipped, or unverified and confirm no new MCP capability or storage-format change occurred.
