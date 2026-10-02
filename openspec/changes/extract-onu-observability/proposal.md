## Why

Production-grade observability is currently embedded inside `AgentSession.MCP`, which makes its privacy controls, bounded telemetry pipeline, failure isolation, and operational assets difficult to reuse in other .NET services. Extracting those capabilities into an independently versioned `OnuObservability` repository and NuGet package family will provide a consistent, maintainable foundation for ASP.NET Core applications, MCP servers, and Generic Host background services without copying application-specific code.

## What Changes

- Create a separate repository and solution named `OnuObservability` with clean architecture, explicit SOLID boundaries, one-way project dependencies, small public interfaces, and independently testable policies and adapters.
- Publish a package family rooted at `OnuObservability`: a stable core, `OnuObservability.Hosting` for Generic Host and `BackgroundService` workloads, `OnuObservability.AspNetCore` for web request integration, and `OnuObservability.Mcp` for MCP request/tool integration. Consumers install only the packages required by their application type.
- Extract reusable configuration validation, resource identity, typed outcomes, safe attribute/log policies, bounded rate limiting, OpenTelemetry provider/exporter setup, pipeline health, and shutdown behavior from `AgentSession.MCP` while keeping application-domain instrumentation outside the shared library.
- Provide first-class background-service operation scopes for scheduled or continuous work, including cancellation, deadline, dependency failure, retry, duration, work-count, and graceful-shutdown telemetry without emitting one record per processed item.
- Preserve metadata-only telemetry, bounded cardinality, asynchronous OTLP export, sampling-independent metrics/security events, fail-open request/work execution, and collector-neutral deployment patterns.
- Preserve the existing `mcp-observability` signal contract and stdio safety while migrating `AgentSession.MCP` to consume published `OnuObservability` packages instead of local duplicated infrastructure.
- Add deterministic NuGet packing and release controls, semantic versioning and API-compatibility checks, symbols and Source Link, dependency and license scanning, package provenance/SBOM evidence, and prerelease validation against representative Web, background-service, and MCP consumers.
- Add focused samples, reference documentation, migration guidance, contract tests, failure-injection tests, and performance budgets for each supported adapter. Console-specific integration and samples are explicitly excluded.

## Capabilities

### New Capabilities

- `onu-observability`: Reusable, privacy-first observability packages and adapters for ASP.NET Core applications, MCP servers, and Generic Host background services, including configuration, telemetry safety, lifecycle resilience, packaging, compatibility, and operational readiness.

### Modified Capabilities

None. The existing `mcp-observability` requirements remain the behavioral compatibility baseline; extraction and package consumption change ownership and implementation rather than the externally observable MCP telemetry contract.

## Impact

- A new `OnuObservability` repository/solution and NuGet release pipeline will become the source of truth for reusable observability code, package documentation, samples, and collector-neutral assets.
- `AgentSession.MCP` will replace local shared observability infrastructure with package references and keep only memory-domain instrumentation, MCP composition, service-specific schemas, and application-owned operational policy that does not belong in a general library.
- New public APIs will include stable registration/configuration entry points, typed operation/outcome contracts, safe metadata builders, background-operation scopes, adapter hooks, and test-support surfaces; public API review and semantic-versioning policy will apply.
- Package dependency direction will prevent the core from referencing ASP.NET Core, Model Context Protocol, application-domain types, or vendor backends. Adapter packages may depend inward on the core/hosting packages and their respective framework SDK only.
- Existing observability tests and documentation will be divided between package-level conformance tests and AgentMemoryMCP consumer/parity tests. Migration must demonstrate equivalent MCP schemas, signals, privacy guarantees, failure isolation, startup validation, shutdown bounds, and published-binary behavior before local implementations are removed.
- Supported consumers are ASP.NET Core web applications, .NET Generic Host background services, and .NET MCP servers. A console-specific adapter is out of scope; a console application using Generic Host may consume the hosting package without receiving a dedicated integration surface.
