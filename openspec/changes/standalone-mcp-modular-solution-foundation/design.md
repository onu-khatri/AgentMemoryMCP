## Context

The repository currently builds four projects from the root: one production executable (`AgentSession.MCP`), one broad test project, a test worker, and benchmarks. The production project combines process startup, stdio MCP registration, protocol contracts and tools, session behavior, memory behavior, filesystem persistence, Qdrant/Ollama integrations, configuration, and observability. See `proposal.md` for the motivation and `specs/standalone-mcp-platform-foundation/spec.md` for the required outcome.

The architecture authority is `UserStory/References/standalone-mcp-server-architecture-requirements-dotnet10.md`. Its requirements apply as a whole: the recommended solution structure, layer responsibilities, dependency direction, stateless HTTP model, request context, security, tool rules, validation, cancellation, resilience, observability, testing, CI/CD, deployment, module independence, explicit non-goals, and definition of done. Folder markers inside the existing executable are not an acceptable substitute for separate projects.

The migration must preserve established MCP tool names and schemas, canonical repository-scoped filesystem data, restart/checkpoint behavior, semantic-service degradation behavior, and the existing stdio package while introducing the independently deployable HTTP platform. No new MCP tool, resource, prompt, database, cache, queue, task store, or record format is part of this change.

## Goals / Non-Goals

**Goals:**

- Replace the monolithic production project with a real multi-project build graph that enforces Host, Protocol, Core, module, Infrastructure, Service Defaults, and test ownership.
- Keep Core stable, small, infrastructure-independent, and free of MCP/ASP.NET/provider SDK types.
- Preserve vertical capability ownership: Session and AgentMemory remain independently understandable and testable.
- Preserve current behavior and public MCP contracts while moving code to its target owner.
- Add a standalone ASP.NET Core Streamable HTTP host with stateless-by-default handling and a temporary stdio compatibility host.
- Centralize SDK/build/package configuration and enforce dependency rules with architecture tests over actual project and assembly references.
- Satisfy the reference document's operational, security, observability, testing, CI, and deployment requirements without introducing speculative abstractions.

**Non-Goals:**

- Adding or changing MCP tools, resources, prompts, schemas, or capability semantics.
- Rewriting session or memory algorithms while relocating them.
- Introducing SQL, Redis, queues, durable tasks, new distributed locks, or a new storage format.
- Completing the future semantic-recall slice or changing Ollama/Qdrant behavior.
- Selecting a production identity provider, Kubernetes platform, or cloud-specific deployment.
- Creating one microservice per module or an interface for every concrete class.

## Decisions

### 1. Use separate projects as the architectural boundary

Adopt this target structure, retaining the existing `AgentSession.MCP` naming family to minimize namespace and package churn:

```text
src/
  AgentSession.MCP.Host/
  AgentSession.MCP.StdioHost/
  AgentSession.MCP.Protocol/
  AgentSession.MCP.Core/
  AgentSession.MCP.Infrastructure/
  AgentSession.MCP.ServiceDefaults/
  Modules/
    Session/
      AgentSession.MCP.Session.Contracts/
      AgentSession.MCP.Session/
    AgentMemory/
      AgentSession.MCP.AgentMemory.Contracts/
      AgentSession.MCP.AgentMemory/

tests/
  AgentSession.MCP.UnitTests/
  AgentSession.MCP.IntegrationTests/
  AgentSession.MCP.ContractTests/
  AgentSession.MCP.ArchitectureTests/
  AgentSession.MCP.EndToEndTests/
  AgentSession.MCP.TestWorker/

benchmarks/
  AgentSession.MCP.Benchmarks/

deploy/
  Dockerfile
  compose.yaml
  environments/

Directory.Build.props
Directory.Packages.props
global.json
AgentMemoryMCP.slnx
```

Each listed production boundary is a `.csproj`; a directory or marker type alone does not satisfy the design. The existing executable is migrated into `AgentSession.MCP.StdioHost` with its package ID/command compatibility retained during transition. `AgentSession.MCP.Host` is the independently deployable HTTP composition root.

The rejected alternative is keeping one `.csproj` and using folders/namespaces plus metadata tests. That cannot enforce NuGet/project dependency direction, allows accidental provider references from Core/Protocol, and directly conflicts with the reference non-goal against putting all code in one project.

### 2. Enforce an explicit project-reference matrix

Use this allowed graph; any reference not listed is prohibited:

| Project | Allowed project references |
| --- | --- |
| `AgentSession.MCP.Core` | none |
| `AgentSession.MCP.Session.Contracts` | `Core` |
| `AgentSession.MCP.AgentMemory.Contracts` | `Core` |
| `AgentSession.MCP.Session` | `Core`, `Session.Contracts` |
| `AgentSession.MCP.AgentMemory` | `Core`, `AgentMemory.Contracts` |
| `AgentSession.MCP.Infrastructure` | `Core` |
| `AgentSession.MCP.Protocol` | `Core`, `Session.Contracts`, `AgentMemory.Contracts` |
| `AgentSession.MCP.ServiceDefaults` | none of the capability/module projects |
| `AgentSession.MCP.Host` | `Core`, `Protocol`, both module implementations, `Infrastructure`, `ServiceDefaults` |
| `AgentSession.MCP.StdioHost` | same composition dependencies as Host, without Host-to-Host coupling |

Core and contracts remain free of ASP.NET Core, MCP SDK, OnuObservability hosting adapters, Qdrant, Ollama HTTP implementation, and filesystem implementation dependencies. Protocol references module contracts, never module implementation projects. Modules do not reference one another or Protocol. Host projects are the only composition roots permitted to reference implementations across layers.

Architecture tests parse `.slnx`/`.csproj` references and inspect compiled assemblies. Tests reject prohibited package references as well as prohibited project references; hand-authored edge fixtures are supplemental only.

### 3. Use Core for stable clean-architecture seams, not shared feature code

Core contains only concepts that are stable across transports and implementations:

- `Abstractions/`: durable storage, time, identity/request context, and execution seams that have more than one meaningful implementation or isolate an external boundary.
- `Context/`: client, user/tenant when applicable, scope, repository resource, and correlation context.
- `Errors/`: normalized error/result categories independent of MCP and infrastructure exceptions.
- `Execution/`: cancellation-aware capability execution contracts and metadata.
- `Policies/`: bounds, idempotency, retry eligibility, and capability policies that are truly cross-module.
- `Security/`: authorization requirements and resource descriptors without ASP.NET authorization types.

Module DTOs and rules do not move into generic Core collections. Existing helper/interfaces are evaluated individually: a type moves to Core only when it represents a stable platform seam; otherwise it stays with Session, AgentMemory, Protocol, or Infrastructure. No generic `Common`, `Shared`, `Helpers`, or `Utils` project is introduced.

### 4. Keep vertical module contracts separate from implementations

Each module has a contracts project and an implementation project:

- Contracts define capability interfaces, requests, responses, public result semantics, registration descriptors, and no provider implementation.
- Implementation contains orchestration, domain/capability rules, and module-specific adapters. Provider-specific infrastructure used by only AgentMemory, including Qdrant and Ollama implementations, remains inside AgentMemory rather than polluting shared Infrastructure.
- Each module exposes one explicit registration extension consumed by Host composition.

Session owns activation/listing, append-to-session, task coordination, resume snapshots/cursors, checkpoints, and their canonical session persistence behavior. AgentMemory owns reusable memory status/remember/get/update/recall/delete/archive/curation/indexing behavior and semantic providers. A module may collaborate only through an approved Core or contracts abstraction; concrete implementation references are forbidden.

The rejected alternative is a single project per module containing MCP attributes, contracts, provider SDKs, and business behavior. That would improve over the monolith but still couple Protocol to module implementations and make transport-independent contract tests harder.

### 5. Make Protocol the only MCP SDK adapter layer

Move all MCP attributes, server tool types, protocol validation/mapping, authorization filters, capability visibility, and protocol error mapping to `AgentSession.MCP.Protocol`. Protocol tool classes are narrow adapters grouped by module but do not become module implementations.

Explicit tool registration is authoritative. A contract snapshot records the current tool names, descriptions, schemas, annotations, and structured outputs before migration; post-migration contract tests compare against that baseline. This prevents refactoring from silently adding, removing, or changing capabilities.

Protocol passes `CancellationToken` and a platform-neutral request context to contracts. It maps normalized Core/module failures to stable MCP errors and never catches provider exceptions to expose them directly.

### 6. Use two composition roots during compatibility migration

`AgentSession.MCP.Host` is an ASP.NET Core `WebApplication` using Streamable HTTP, stateless-by-default request handling, authentication/authorization, rate limiting, health/readiness, Service Defaults, explicit module/tool registration, and external configuration.

`AgentSession.MCP.StdioHost` retains the existing packaged stdio command and tool surface while consumers migrate. It uses the same Core, Protocol, modules, Infrastructure, and registration extensions; it does not duplicate capability logic. The compatibility host can be removed only in a later explicitly approved breaking change.

Both hosts stay small and contain only composition/startup/shutdown behavior. The HTTP host introduces a standards-based local test issuer configuration and policy wiring but does not select a production identity provider. Tool listing/invocation authorization is centralized in Protocol/Host filters rather than duplicated inside tools.

### 7. Separate shared Infrastructure from module-specific adapters

`AgentSession.MCP.Infrastructure` owns implementations genuinely reused by multiple modules: canonical root/path safety, atomic/managed transactions, shared repository locking primitives, platform filesystem access, cross-cutting resilience primitives, and observability adapters. It implements Core abstractions and never references Protocol or Host.

Session-specific persistence remains in Session; AgentMemory-specific canonical record/index/embedding behavior remains in AgentMemory. Moving code is not an opportunity to rewrite storage semantics. Existing canonical paths, record formats, transaction/locking behavior, snapshot/cursor behavior, and recovery rules remain unchanged.

### 8. Centralize deterministic build and package management

Add:

- `global.json` pinning the supported .NET 10 SDK with an intentional roll-forward policy.
- `Directory.Build.props` for target framework, nullable, implicit usings, deterministic builds, analyzers, warnings policy, and repository metadata shared across projects.
- `Directory.Packages.props` with central package management and every external package version currently spread across projects.

Project files declare package identities only. Runtime identifiers, single-file/self-contained packaging, package metadata, and MCP server packaging stay on the appropriate host project rather than leaking into libraries. The `.slnx` lists all production, test, worker, and benchmark projects in architectural groups. Service Defaults are included, but an Aspire AppHost is deferred because Docker Compose provides the required local orchestration without adding another runtime dependency to the initial foundation.

### 9. Treat security, cancellation, failures, resilience, and observability as end-to-end behavior

Host establishes authenticated request context and policy/scope authorization. Protocol validates untrusted schemas, bounds, and capability visibility. Core expresses platform-neutral context, policies, and normalized errors. Modules enforce capability/resource rules. Adapters propagate cancellation to filesystem, HTTP, Qdrant, and other dependencies.

Use validated Options with fail-fast startup, typed `HttpClient` and safe resilience policies, structured redacted logging, OpenTelemetry traces/metrics, bounded dimensions, dependency health, graceful shutdown, and client/module/tool-aware rate limiting. OnuObservability integration stays at Host/Infrastructure/Protocol boundaries; Core does not reference its hosting or MCP packages.

### 10. Split verification by purpose and verify real boundaries

- Unit tests cover Core policies, module behavior, validation, mapping, and normalized failures without external services.
- Integration tests cover canonical filesystem, module adapters, external-provider adapters, configuration, and authentication using realistic disposable infrastructure where applicable.
- MCP Contract tests use an independent client to verify tool discovery, schemas, authorization, validation, cancellation, errors, and stdio/HTTP compatibility.
- Architecture tests inspect `.slnx`, `.csproj`, NuGet references, namespaces/assemblies, composition roots, and consumer isolation.
- End-to-End tests start the independently deployable host/container and exercise an authorized existing tool flow, restart continuity, health/readiness, telemetry, and applicable replica behavior.

The existing broad test project is decomposed by responsibility. `TestWorker` remains a test-support executable under `tests/`; benchmarks move under `benchmarks/` and reference only the assemblies they measure.

## Risks / Trade-offs

- [Large mechanical migration obscures behavior changes] → Capture contract/storage baselines first, move one ownership slice at a time, build after every project-reference change, and prohibit opportunistic algorithm refactors.
- [Circular references emerge from current concrete dependencies] → Identify each cycle explicitly, move only stable seams to Core/contracts, and keep concrete composition in hosts; do not resolve cycles with a generic shared project.
- [Contracts projects become DTO dumping grounds] → Keep contracts module-owned, public-surface focused, and free of implementation helpers or provider types.
- [Dual hosts drift] → Share registrations and protocol adapters; test contract parity; keep hosts composition-only.
- [Provider packages leak into Core/Protocol] → Enforce package and assembly rules in ArchitectureTests and central package ownership.
- [Canonical filesystem cannot provide general multi-node guarantees] → Preserve current semantics and scope replica claims to shared storage arrangements actually verified; do not claim universal distributed-filesystem safety.
- [Central package migration changes resolved versions] → Record the current dependency graph/lock evidence and move versions without upgrades unless a build blocker is separately approved.
- [Authentication transport work introduces capability changes] → Snapshot tools before migration and reject any discovery/schema/semantic delta not explicitly approved.
- [Migration breaks package consumers] → Preserve `PackageId`, command metadata, and stdio compatibility until a separately versioned migration is approved.

## Migration Plan

1. Capture baseline evidence: restore/build/tests, MCP discovery/schema snapshots, package metadata, canonical path/record fixtures, and current project/package graph.
2. Add central SDK/build/package files and create empty buildable target projects plus dedicated test projects in `AgentMemoryMCP.slnx`; add architecture tests for the intended graph before moving behavior.
3. Extract Core and module contracts first, resolving concrete cycles through the smallest meaningful abstractions; keep the existing executable compiling through temporary project references.
4. Extract shared Infrastructure implementations and module implementations one ownership slice at a time, preserving canonical storage behavior and running focused regression tests after each move.
5. Extract Protocol adapters and explicit tool registration; validate the saved MCP contract baseline and cancellation/error mapping.
6. Create the HTTP Host and Service Defaults, then refactor the existing executable into the StdioHost compatibility composition root using the same registrations.
7. Split existing tests into Unit, Integration, Contract, Architecture, and End-to-End projects; move worker/benchmark projects to their target locations.
8. Add Docker/Compose assets; verify health/readiness, graceful shutdown, restart continuity, an authorized MCP flow, and supported replica behavior.
9. Update CI and documentation, run the full Release/security/container/E2E matrix, and record unavailable environment-dependent evidence separately.

Rollback during migration uses small commits/checkpoints: restore the previous composition/project step while retaining unchanged canonical data. No data rollback or record transformation is required. The stdio host remains the compatibility fallback until the HTTP path is verified and separately approved for consumer migration.

## Open Questions

- Production identity-provider selection and cloud deployment remain deferred; local/CI authentication must use provider-neutral validated configuration.
