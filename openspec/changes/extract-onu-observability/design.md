## Context

See [proposal.md](proposal.md) for motivation and [the capability spec](specs/onu-observability/spec.md) for normative behavior.

The current implementation is embedded in a .NET 10 MCP executable. Its reusable observability area contains 22 source files and approximately 1,800 lines covering validated options, resource identity, schema registries, outcome classification, safe logs, rate limiting, guarded exporters, pipeline health, local activity support, and bounded shutdown. The same area also contains MCP SDK filters, tool discovery, `mcp.*` instruments, and application-specific dependency operations. Registration currently combines all of those concerns, clears logging providers, assumes stdio, uses MCP-specific option limits, and directly references OpenTelemetry and Model Context Protocol packages.

The existing tests already exercise configuration, schema bounds, redaction, cardinality, request/tool filters, exporter failure, shutdown, and background work. The existing `mcp-observability` specification is the compatibility baseline. Extraction must preserve those guarantees while making the reusable portion suitable for ASP.NET Core and Generic Host background services. AgentMemoryMCP must continue supporting self-contained and single-file publishing.

The repository host and owner are approved as `github.com/onu-khatri/OnuObservability`, and the standalone local checkout now exists at `D:\RND\ONU\OnuObservability` beside AgentMemoryMCP. The external package feed, release credentials, signing mechanism, publication, and stable promotion still require separate authorization.

## Goals / Non-Goals

**Goals:**

- Establish an understandable package topology with dependencies that point inward and framework adapters that remain replaceable.
- Keep the stable public surface small, typed, documented, and testable without exposing OpenTelemetry SDK internals.
- Separate reusable telemetry policy from ASP.NET Core, MCP, and AgentMemory domain concerns.
- Make background-service passes and continuous work first-class, with correct cancellation and coarse aggregate telemetry.
- Preserve MCP signal names, privacy behavior, stdio purity, export resilience, and lifecycle bounds during migration.
- Produce NuGet artifacts that are validated as artifacts, not merely as project references, before release or adoption.

**Non-Goals:**

- A console-specific adapter, console sample, proprietary backend SDK, telemetry UI, or hosted collector service.
- Generic automatic instrumentation of arbitrary business methods, every queue item, or every file/database operation.
- Content capture, arbitrary baggage propagation, dynamic labels derived from caller data, or telemetry as a regulated audit system of record.
- Moving AgentMemory storage, tool, Ollama, Qdrant, or maintenance-domain logic into the shared repository.
- Preserving the current internal class names or making every existing internal helper public.

## Decisions

### 1. Use one repository with four production packages

The repository and solution are named `OnuObservability`. The initial production projects are:

```text
OnuObservability
+-- src/OnuObservability
+-- src/OnuObservability.Hosting
+-- src/OnuObservability.AspNetCore
+-- src/OnuObservability.Mcp
+-- tests/<matching unit, architecture, contract, and integration projects>
+-- samples/OnuObservability.Sample.Web
+-- samples/OnuObservability.Sample.Worker
+-- samples/OnuObservability.Sample.Mcp
+-- eng/<pack, API compatibility, SBOM, and release tooling>
```

Dependency direction is fixed:

```text
AspNetCore --------> Hosting --------> Core
Mcp --------------> Hosting --------> Core
Sample/consumer ---> selected adapter packages only
```

`OnuObservability` contains bounded value objects, descriptors, outcomes, schema policy, rate limiting, and SDK-independent operation contracts. It may depend on BCL diagnostics plus narrow `Microsoft.Extensions.*.Abstractions` packages, but not on the OpenTelemetry SDK, ASP.NET Core, MCP, or application types. `OnuObservability.Hosting` owns configuration binding, provider/exporter composition, resource construction, export health, and lifecycle flushing. Framework packages add only their adapter and schema.

All packages, samples, tests, and benchmarks use a `net10.0` API baseline and are validated with the .NET 10 SDK/runtime on Windows and Linux. An additional target framework is added only through an explicit compatibility decision. Dropping or adding a supported runtime follows the package compatibility policy and requires the appropriate semantic-version change.

Alternative considered: one package containing every integration. Rejected because it violates interface segregation, introduces unnecessary framework dependencies, and makes security/compatibility review harder. Alternative considered: many fine-grained packages for every primitive. Rejected because the restore and versioning burden would exceed the current reuse benefit.

### 2. Enforce clean architecture and SOLID at compile time

The core is organized by responsibility rather than by framework:

- **Policy/domain:** immutable descriptors, outcomes, error/reason registries, bounds, and schema versions.
- **Application ports:** small operation, clock, pipeline-health, and schema-registration contracts.
- **Infrastructure:** OpenTelemetry processors/exporters, configuration providers, and host lifecycle implementations in `Hosting`.
- **Inbound adapters:** ASP.NET Core middleware/instrumentation enrichment, MCP SDK filters, and background-operation helpers.

Single responsibility is enforced by keeping validation, logging policy, resource identity, exporter health, and each adapter separate. Open/closed behavior comes from startup registration of immutable descriptors rather than switches in the core. Substitutable implementations use explicit outcome semantics and deterministic tests. Interface segregation keeps consumers from depending on exporters or adapter internals. Dependency inversion keeps the core and consumer domain code independent of OpenTelemetry SDK types.

Architecture tests reject references from core to hosting/framework packages, cross-adapter references, public framework leakage, service-location patterns, and mutable global state. Public constructors remain dependency-injection friendly; internal implementation types stay internal. Time-sensitive behavior receives `TimeProvider`, and exporter behavior is tested through narrow ports.

Alternative considered: copy the current namespace into a class library and make its types public. Rejected because the current registration, schema, and dependency telemetry combine reusable, MCP, and AgentMemory responsibilities.

### 3. Expose one builder with typed, immutable schemas

The registration entry point returns an `OnuObservabilityBuilder`. Adapter packages contribute extension methods to the same builder, so composition reads as one coherent setup while package references control which methods exist. Registration is idempotent for identical settings and fails startup for conflicting schema names, duplicate descriptors with different definitions, or incompatible provider ownership.

The public model uses immutable concepts such as an operation descriptor, event descriptor, outcome, registered error/reason code, and bounded field definition. A descriptor fixes names, allowed fields, value kinds, maximum lengths/counts, and finite value registries at startup. Runtime emission accepts the descriptor and typed values; it does not accept arbitrary object bags, `Activity`, `TagList`, log-state dictionaries, exporter processors, or exception instances. Unknown values map to a descriptor-owned fallback or are dropped.

Application code instruments domain operations through a narrow operation factory/scope. The scope supports explicit success, bounded failure classification, aggregate counts, and disposal-based duration. It does not expose raw OpenTelemetry SDK types. Consumers that already use `ActivitySource`, `Meter`, and `ILogger` can register reviewed sources/meters/categories with the hosting builder, but those records remain subject to the same provider policy.

Alternative considered: accept `Dictionary<string, object?>` to maximize flexibility. Rejected because it defeats privacy review, cardinality guarantees, and API discoverability. Alternative considered: a base class for observed services. Rejected because composition and small interfaces are easier to adopt and test than inheritance.

### 4. Separate common, adapter, and application schemas

The core schema covers only package pipeline-health, suppression, lifecycle, and common outcome semantics under an `onu.*` namespace. The hosting package owns resource identity and exporter health. ASP.NET Core primarily uses stable OpenTelemetry HTTP semantic conventions plus a small versioned adapter schema. The MCP adapter owns the existing `mcp.*` catalog and copies its versioned names, units, buckets, and bounded value registries from the AgentMemoryMCP compatibility baseline.

AgentMemory-specific dependency types, operations, maintenance counts, tool policy, application schema version, and toolset version remain in AgentMemoryMCP and are registered through the public descriptor API. This keeps a reusable package from knowing about Ollama, Qdrant, memory records, repositories, or the server's 26-tool catalog.

Schema manifests are generated or validated from one declarative source per package and used by runtime registration, documentation, public API fixtures, and conformance tests. Additive schema changes advance the minor schema version; meaning changes or removals require a major schema version and consumer migration guidance.

Alternative considered: rename all MCP signals to generic names. Rejected because it would break the newly established dashboards, tests, and operational contract without improving Web or worker observability.

### 5. Layer configuration without taking over the host

`OnuObservabilityOptions` contains common enabled-signal, exporter, sampling, batch, queue, timeout, shutdown, logging, resource, and safety limits. `AspNetCoreObservabilityOptions` and `McpObservabilityOptions` contain only adapter-specific policy. Application schemas own their own limits such as MCP argument/result counts. Options bind from an `OnuObservability` section, then apply the supported `OTEL_*` precedence, then validate the effective immutable snapshot on host startup.

The library never calls `ClearProviders`, silently changes the global minimum log level, or changes the host's overall shutdown timeout. It adds only its reviewed provider/processors and category filters. JSON console/stderr output is an explicit hosting option; the MCP adapter enables and validates the protocol-safe stderr profile during MCP composition. Exporter headers remain under the standard OpenTelemetry configuration path and are never read back for summaries or errors.

The package owns one bounded flush budget for its providers. A host may configure a longer global shutdown timeout, but the observability component cannot consume more than its own budget.

Alternative considered: preserve the current registration verbatim. Rejected because clearing providers and changing `HostOptions` are acceptable application composition choices but unsafe behavior for a reusable library.

### 6. Integrate Web requests by enriching one framework-owned span

The ASP.NET Core adapter composes supported OpenTelemetry ASP.NET Core instrumentation and enriches the single server activity after routing. It automatically discovers stable route templates from ASP.NET Core endpoint data sources, sorts and retains at most 1,024 templates, collapses overflow to `other`, and emits one count-only warning when the cap is exceeded. Exact route-template exclusions bind from `OnuObservability:AspNetCore:ExcludedRouteTemplates`; consumers may lower but not raise the 1,024-template cap. It records route templates rather than raw paths or route values, maps HTTP status/cancellation/exception state to bounded outcomes, and emits adapter metrics and at most one reviewed boundary log. Header, query, body, cookie, client-address, and exception recording remain disabled.

Provider/source inspection tests ensure the adapter does not create a second server span when framework instrumentation is active. If a consumer has already registered compatible ASP.NET Core instrumentation, the adapter contributes policy/enrichment once rather than adding a duplicate listener. Health/static endpoints can be excluded only through registered route patterns, never raw request values.

Alternative considered: custom middleware that always starts a new server activity. Rejected because it duplicates standard instrumentation and propagation. Alternative considered: automatic request/response logging. Rejected because body/header capture conflicts with metadata-only defaults.

### 7. Model background work as explicit coarse operations

`OnuObservability.Hosting` provides an operation runner/scope for an entire scheduled pass, queue batch, reconciliation, or other bounded work unit. Consumers register a finite operation descriptor at startup and supply only aggregate counts/outcomes at completion. Continuous services define their own repeat boundary; the library does not guess by instrumenting `ExecuteAsync` as one process-long success span or by creating a span per item.

Cancellation classification receives both the host stopping token and any operation deadline/caller token so host shutdown, caller cancellation, and deadline expiry remain distinct. Retry/deferred outcomes are explicit bounded states, not inferred from exception messages. Dependencies may create child operations using registered dependency descriptors. Completion is exception-safe and exactly once even when consumer code throws.

Alternative considered: a required `ObservableBackgroundService` base class. Rejected because it couples consumers to inheritance and cannot represent hosted services, scheduled callbacks, or message handlers uniformly.

### 8. Keep MCP behavior in an adapter and application extension layer

`OnuObservability.Mcp` contains the MCP incoming-message and tool-call filters, protocol/method normalization, registered tool descriptor bridge, typed outcome channel, and stdio-safe logging profile. It targets the supported Model Context Protocol package version and keeps that dependency private to MCP consumers. The adapter maps SDK primitives into the unchanged `mcp.*` schema without inspecting argument/result values.

Tool schemas, destructive hints, and toolset version come from the same SDK descriptors used by `tools/list`. The consumer supplies application error/reason mappings through registered bounded descriptors. AgentMemoryMCP retains the translation from its domain exceptions into those public bounded outcomes and retains its dependency/maintenance descriptors. No exception-message parsing or static application callback is introduced.

An MCP adapter compatibility test fixture is run against both legacy and current supported protocol models. Stdout-purity process tests use the packed package and the published MCP executable.

Alternative considered: keep MCP filters in AgentMemoryMCP and extract only exporters. Rejected because request/tool boundary policy is reusable by other MCP servers and is the primary place where privacy and duplicate-event guarantees are enforced.

### 9. Make export failure isolation generic and observable

The hosting package owns one activity source/meter for its pipeline and configures parent-based sampling, explicit histogram views, bounded batch processors/readers, OTLP gRPC or HTTP/protobuf export, and optional safe logs. A guarded exporter records only signal type and registered failure reason through a non-recursive diagnostics sink and unsampled pipeline meter. It never depends on MCP instruments.

A lightweight listener keeps local activities available when remote export is disabled. HTTP instrumentation is opt-in through adapter/consumer-reviewed filters; no package-wide network capture is enabled. Shutdown flushes logs, traces, and metrics concurrently where safe but applies one wall-clock budget to the whole operation, then permits disposal/process exit even for a non-cooperative exporter.

Collector configurations remain vendor-neutral templates. Persistent queues remain a separate opt-in overlay with explicit retention and security warnings.

Alternative considered: rely entirely on default OpenTelemetry batch processors and diagnostics. Rejected because the current requirements need bounded application-visible drop/failure evidence and a total shutdown limit. The wrapper remains deliberately thin to reduce divergence from upstream SDK behavior.

### 10. Validate packaged artifacts and public compatibility

The repository uses central package version management, pinned SDK/tooling versions, locked restore in CI, nullable analysis, analyzers, warnings-as-errors for production projects, deterministic builds, repository metadata, Source Link, symbols, license files, README content, and package-content allowlists. Package validation and a checked-in public API baseline detect binary/API changes. Architecture tests validate dependency direction from compiled assemblies rather than directory names.

CI creates packages once, stores hashes and SBOM/provenance evidence, and uses those exact `.nupkg` files for isolated-feed consumer tests. Project-reference tests remain useful during development but cannot satisfy the release gate. Representative Web, Worker, and MCP samples verify restore, build, startup, signal export, privacy, failure isolation, shutdown, trimming/single-file compatibility where declared, and performance budgets on supported Windows and Linux runners.

Stable publishing requires an immutable version and approval for the target feed. Signing follows the selected feed's approved signing mechanism; credentials are injected only by the release environment. A missing signing/provenance requirement blocks publication rather than falling back silently.

Alternative considered: publish directly from the main build after unit tests. Rejected because packing can change dependency assets, metadata, analyzers, content, and runtime behavior.

### 11. Split ownership without duplicating documentation

`OnuObservability` owns package APIs, generic configuration, common schemas, Web/Worker/MCP adapter behavior, package release guidance, collector templates, and reusable runbooks. AgentMemoryMCP owns its service identity, domain descriptors, MCP tool registry, dashboards/SLO thresholds, deployment configuration, and consumer migration evidence. AgentMemoryMCP documentation links to the package reference and documents only its overrides and operational decisions.

The source inventory records every existing observability file as **move/generalize**, **MCP adapter**, **AgentMemory-owned**, or **retire after parity** before code extraction begins. This prevents blind copying and makes deletion reviewable.

## Risks / Trade-offs

- **[Premature generic APIs]** A reusable surface can become an abstraction over one application. **Mitigation:** validate every public contract with Web, Worker, and MCP consumers; keep application schemas outside core; use API review and prerelease feedback before `1.0.0`.
- **[Package fragmentation]** Four packages add version and dependency coordination. **Mitigation:** release them as one aligned version family, use central versioning, and let consumers install an adapter that transitively brings the required inward packages.
- **[Framework version drift]** ASP.NET Core, OpenTelemetry, or MCP SDK changes can break adapters independently. **Mitigation:** isolate adapters, pin build inputs, test the support matrix, and publish compatibility updates without changing core policy unnecessarily.
- **[Behavior drift during extraction]** Refactoring could rename signals, loosen redaction, duplicate spans, or alter shutdown. **Mitigation:** run the same golden schema/privacy/process fixtures against old and packaged implementations before deleting local code.
- **[Overly restrictive safety API]** Typed descriptors require more setup than arbitrary tags. **Mitigation:** ship discoverable builders, documented defaults, adapter conventions, analyzers where valuable, and focused samples while retaining fail-closed behavior.
- **[Library takes over host policy]** Logging/provider or lifetime configuration could surprise consumers. **Mitigation:** never clear providers or alter global host settings; make local output explicit and scope all lifecycle limits to package-owned providers.
- **[Background span explosion]** Consumers may choose an operation boundary that is too fine. **Mitigation:** document coarse pass/batch semantics, provide aggregate counters, test high-volume samples, and add diagnostic warnings for unbounded descriptor registration rather than item values.
- **[External repository/feed unavailable]** Implementation cannot publish or consume a package without owner, URL, permissions, and credentials. **Mitigation:** complete local isolated-feed validation first, then stop at an explicit release checkpoint until external details are approved.
- **[Target-framework support cost]** A broad runtime baseline increases CI and dependency constraints. **Mitigation:** target only `net10.0`, test it on Windows and Linux, publish the support window, and add another runtime only through the compatibility policy.

## Migration Plan

1. Record the approved external repository location, owner, package feed, namespace ownership, release signing/provenance policy, and initial prerelease version; do not publish until these are explicit.
2. Inventory current files, public behavior, package references, signal manifests, configuration keys, docs, collector assets, and tests. Classify each item as core, hosting, Web adapter, MCP adapter, AgentMemory-owned, or retire-after-parity.
3. Bootstrap the `OnuObservability` solution, project graph, analyzers, architecture tests, package metadata, CI, samples, and local isolated feed without modifying AgentMemoryMCP runtime composition.
4. Extract core policy and hosting infrastructure behind the typed public contracts. Port generic tests and establish API/schema baselines.
5. Implement and validate the background-service, ASP.NET Core, and MCP adapters. Run their tests against packed prerelease artifacts and the local collector/failure profiles.
6. Publish an approved prerelease to the selected feed. In AgentMemoryMCP, replace local reusable/adapter infrastructure with package references while retaining application-domain descriptors and mappings. Do not register old and new providers simultaneously.
7. Run the existing complete AgentMemoryMCP suite, Web/Worker/MCP consumer suites, schema diff, synthetic-secret corpus, cardinality tests, collector failure/recovery tests, published RID smoke tests, single-file checks, and overhead benchmarks. Record exact pass/fail/skip totals.
8. Remove local implementations only after parity passes against the exact package. Update ownership-focused documentation and keep a source-to-package migration map for review.
9. Promote the same validated artifacts to a stable release only after API, dependency, license, SBOM/provenance, signing, supported-runtime, and production-approval gates pass.

Rollback before stable promotion restores the AgentMemoryMCP commit that references the last known-good implementation/package. After stable adoption, rollback pins the previous known-good NuGet family and application release. Configuration keys and collector contracts remain compatible across the rollback window; a breaking configuration/schema migration requires a separate change with dual-read or explicit conversion guidance.

## Open Questions

- Which NuGet feed and promotion channels will be used, and what signing/provenance mechanism does that feed require?
- No additional target framework is currently approved; the initial package family supports `net10.0` only.
