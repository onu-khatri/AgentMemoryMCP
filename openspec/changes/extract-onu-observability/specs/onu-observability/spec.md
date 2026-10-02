## Purpose

Provide reusable, privacy-first observability packages for ASP.NET Core applications, MCP servers, and Generic Host background services with stable contracts, bounded telemetry, resilient OTLP export, and independently verifiable NuGet releases.

## ADDED Requirements

### Requirement: Selective package composition
The `OnuObservability` package family SHALL expose a stable core plus application-type integrations for Generic Host background services, ASP.NET Core, and MCP. A consumer SHALL be able to install only its required integration without receiving dependencies for unrelated application types, and framework-specific dependencies SHALL remain isolated to the corresponding integration package. The package family SHALL NOT provide a console-specific adapter; console processes that use Generic Host MAY consume the hosting integration.

#### Scenario: Background service installs hosting support
- **WHEN** a Generic Host background-service project references the hosting integration
- **THEN** it receives core telemetry and lifecycle support without ASP.NET Core or MCP SDK dependencies

#### Scenario: Web application installs web support
- **WHEN** an ASP.NET Core project references the web integration
- **THEN** it receives the core and hosting capabilities plus web instrumentation without an MCP SDK dependency

#### Scenario: MCP server installs MCP support
- **WHEN** an MCP server references the MCP integration
- **THEN** MCP-specific request and tool instrumentation is available while the core package remains independent of the MCP SDK

### Requirement: Stable and validated configuration
The packages SHALL provide one strongly typed configuration model for signal enablement, OTLP protocol and endpoint, service identity, environment, sampling, batching, queueing, export timeout, shutdown budget, logging bounds, and cardinality limits. Standard `OTEL_*` values SHALL have documented precedence over package configuration. Invalid or insecure enabled-export configuration SHALL fail during host startup with a bounded message that does not reproduce credentials, headers, query strings, or fragments, while remote export SHALL remain disabled by default.

#### Scenario: Local host uses defaults
- **WHEN** a consumer starts without an exporter endpoint
- **THEN** local instrumentation and approved structured diagnostics remain available without requiring a collector

#### Scenario: Standard environment setting overrides configuration
- **WHEN** a supported `OTEL_*` value and a package configuration value specify the same setting
- **THEN** the validated effective configuration uses the documented standard-variable precedence

#### Scenario: Unsafe endpoint is configured
- **WHEN** enabled export uses credentials in the URI, an invalid protocol, an unbounded queue, or clear-text transport to a non-loopback non-sidecar endpoint
- **THEN** startup fails before serving work and the validation output contains none of the supplied secret-bearing values

### Requirement: Consistent resource and schema identity
Every emitted signal SHALL carry a non-generic service name, service namespace, service version when available, deployment environment, runtime identity, telemetry schema version, and policy version. Traces and logs SHALL carry a per-process instance identifier, while metrics SHALL omit that identifier. Application integrations SHALL add only their documented bounded identity fields, and schema changes SHALL follow a documented compatibility and versioning policy.

#### Scenario: Signals are exported from one process
- **WHEN** traces, metrics, and logs are produced by the same configured consumer
- **THEN** their shared resource fields agree and only traces and logs include the process instance identifier

#### Scenario: Adapter schema evolves additively
- **WHEN** an approved optional field is added without changing existing field meaning
- **THEN** the schema minor version advances and existing dashboards remain valid

### Requirement: Metadata-only safe emission
The packages SHALL accept telemetry only through registered, typed, bounded descriptors and approved attribute/value sources. Prompts, request or response bodies, tool arguments or results, record content, credentials, authorization material, arbitrary headers, query strings, raw URLs, private paths, exception messages, stack traces, and raw user, actor, session, operation, or object identifiers SHALL be excluded before formatting, queueing, or export. Unknown attributes, events, dimensions, and outcome values SHALL fail closed by being dropped or mapped to documented `unknown` or `other` values.

#### Scenario: Consumer submits synthetic sensitive data
- **WHEN** input, nested metadata, an exception, and a dependency response contain synthetic secrets or private content
- **THEN** the values appear in none of the captured logs, spans, metrics, queues, snapshots, or diagnostics

#### Scenario: Unregistered metadata is supplied
- **WHEN** consumer code attempts to attach an attribute or dimension that was not registered in the active telemetry schema
- **THEN** the value is rejected or omitted before it reaches an OpenTelemetry processor or exporter

#### Scenario: Bounded value exceeds its limit
- **WHEN** an approved string or count exceeds its configured bound
- **THEN** the emitted value is safely truncated or clamped with documented bounded evidence and without exposing the discarded content

### Requirement: Correlated structured logging
The packages SHALL support structured, event-oriented logging with stable event identifiers, constant message templates, UTC timestamps, trace/span correlation, approved categories, bounded fields, and bounded rate limiting. Provider filtering SHALL occur before formatting or OpenTelemetry queueing, formatted-message and automatic-exception export SHALL remain disabled, and suppression evidence SHALL not recursively re-enter the failing or suppressed pipeline.

#### Scenario: Approved event is written during an activity
- **WHEN** a registered event is emitted while a trace span is active
- **THEN** local and enabled remote log providers receive equivalent safe fields with trace and span correlation

#### Scenario: Unapproved category writes a record
- **WHEN** framework, SDK, or application code logs through a category or event identifier outside the active policy
- **THEN** the whole record is excluded without inspecting or exporting its body or exception

#### Scenario: Repeated event exceeds its rate
- **WHEN** an approved bounded event key exceeds its configured burst and window
- **THEN** detailed records are suppressed and a bounded non-recursive suppression count becomes observable

### Requirement: ASP.NET Core request observability
The web integration SHALL produce correlated request telemetry using automatically discovered bounded route templates, HTTP method, response status class, duration, and stable outcome values without recording raw paths, route values, query strings, request or response bodies, cookies, authorization headers, client addresses, or exception content. It SHALL retain no more than 1,024 deterministically ordered templates, collapse overflow to `other`, emit a count-only warning when the cap is exceeded, and support exact route-template exclusions through configuration. It SHALL cooperate with supported ASP.NET Core and OpenTelemetry instrumentation so one logical request does not produce duplicate server spans or duplicate completion logs.

#### Scenario: Successful web request
- **WHEN** an instrumented endpoint completes successfully
- **THEN** one logical server request is represented with a bounded route template, status, duration, metrics, and trace-correlated completion evidence

#### Scenario: Sensitive web request fails
- **WHEN** a request containing sensitive route values, query data, headers, and body content fails
- **THEN** telemetry records the bounded failure classification without any of those sensitive values or the exception message

#### Scenario: Framework instrumentation is already active
- **WHEN** the host enables a supported ASP.NET Core OpenTelemetry server instrumentation source
- **THEN** the integration enriches or reuses the logical request trace without creating a duplicate server span

### Requirement: Background-service operation observability
The hosting integration SHALL provide coarse operation telemetry for `IHostedService` and `BackgroundService` workloads, including operation name, duration, outcome, dependency classification, retry/deferred state, and bounded work/result counts. Host cancellation, caller cancellation, deadline expiry, dependency failure, and internal failure SHALL remain distinct. Continuous or batch workloads SHALL emit one operation boundary per configured pass or unit of work rather than one span or log per processed record.

#### Scenario: Scheduled pass succeeds
- **WHEN** a background service completes a configured maintenance pass
- **THEN** one operation span and the corresponding unsampled metrics record success, duration, and bounded aggregate counts

#### Scenario: Host is shutting down
- **WHEN** the host cancellation token stops an in-flight background operation
- **THEN** telemetry records host cancellation rather than an internal failure and shutdown remains within the configured budget

#### Scenario: Work is partially deferred
- **WHEN** a batch processes some items and defers or retries others
- **THEN** one coarse operation reports bounded processed, deferred, retry, and error counts without per-item telemetry or identifiers

### Requirement: MCP request and tool observability
The MCP integration SHALL instrument supported request and tool-call boundaries with stable low-cardinality names, negotiated protocol version, transport, registered tool identity, bounded counts, typed outcomes, and correct trace parentage. It SHALL preserve public MCP schemas and results, SHALL NOT inspect or reserialize argument/result content for telemetry, and for stdio transport SHALL keep stdout protocol-only while routing approved local diagnostics to stderr. Unknown tools and protocols SHALL map to bounded fallback values.

#### Scenario: Registered MCP tool completes
- **WHEN** a configured tool is called through an instrumented MCP server
- **THEN** one request span, one tool span, bounded metrics, and one completion event are produced without changing the tool request or result

#### Scenario: MCP domain failure is translated
- **WHEN** application code records a typed bounded classification before the SDK converts a domain exception to an error result
- **THEN** the tool telemetry retains that classification without parsing rendered client messages

#### Scenario: Stdio server emits telemetry
- **WHEN** the MCP server starts, handles success and failure, exports telemetry, and shuts down
- **THEN** every stdout frame remains MCP protocol traffic and approved local diagnostics remain bounded JSON on stderr

### Requirement: Resilient vendor-neutral export and lifecycle
The hosting integration SHALL configure vendor-neutral OpenTelemetry traces, metrics, and optional logs with asynchronous bounded processors and OTLP export. Collector, network, backend, queue, or exporter failure SHALL NOT fail or indefinitely delay application requests or background work, exhaust memory, or recursively amplify diagnostics. Normal shutdown SHALL attempt to flush healthy queues within one total configured budget and SHALL allow process exit when an exporter ignores cancellation.

#### Scenario: Collector is unavailable
- **WHEN** an enabled collector cannot be reached during request or background processing
- **THEN** application work continues, queue and memory use remain bounded, and bounded pipeline-health evidence is emitted outside the failed export path

#### Scenario: Healthy host shuts down
- **WHEN** the host stops with queued telemetry and a responsive exporter
- **THEN** queued signals are offered for export before the shutdown budget expires

#### Scenario: Exporter hangs during shutdown
- **WHEN** an exporter does not honor its flush cancellation
- **THEN** the process is not delayed beyond the total configured shutdown budget

### Requirement: Sampling and cardinality preserve operational truth
Metrics and required operational security events SHALL remain independent of trace sampling. Trace sampling SHALL preserve valid parent decisions and use only approved bounded inputs. Metric labels, rate-limiter keys, span names, and log dimensions SHALL come from finite registered sets; unique caller-provided values SHALL not create new time series, names, or in-memory keys.

#### Scenario: Root trace is not sampled
- **WHEN** head sampling drops a successful root trace
- **THEN** request, operation, failure, and pipeline-health metrics remain complete

#### Scenario: High-cardinality inputs are exercised
- **WHEN** many requests use unique identifiers, paths, filenames, URLs, exception text, and payload values
- **THEN** the exported dimension sets and internal limiter key count remain within their configured finite bounds

### Requirement: Stable extensibility and dependency inversion
Consumers SHALL extend telemetry through small documented contracts for schema registration, outcome classification, operation boundaries, time, and export-health observation rather than depending on package internals or OpenTelemetry SDK implementation types. Default registrations SHALL be replaceable through dependency injection without service-locator access, global mutable state, or inheritance from framework-specific base classes. Adding an application-owned bounded operation or outcome SHALL not require modification of the core package.

#### Scenario: Application registers a bounded operation
- **WHEN** a consumer defines a new operation using an approved descriptor and finite value registry
- **THEN** the operation participates in the common safety, resource, lifecycle, and export policies without changing core source code

#### Scenario: Test replaces time and exporter health ports
- **WHEN** a test supplies deterministic clock and exporter-health implementations through dependency injection
- **THEN** rate, timeout, retry, and failure behavior can be verified without accessing internal package types

#### Scenario: Consumer attempts to use an SDK-specific internal type
- **WHEN** application code compiles only against the documented public package API
- **THEN** OpenTelemetry processor/exporter implementation details are not required in the application or domain layer

### Requirement: Versioned and verifiable NuGet releases
Each published package SHALL use semantic versioning, deterministic and reproducible packing inputs, immutable version numbers, dependency version policy, package metadata, license information, symbols, source mapping, checksums, and machine-readable provenance or SBOM evidence. Release validation SHALL detect accidental public API breaks, forbidden dependency edges, dependency vulnerabilities, license-policy violations, missing documentation, and package contents that differ from the validated build. A package SHALL be promoted to a stable channel only after representative Web, background-service, and MCP consumers pass compatibility, privacy, resilience, and performance checks against the exact packed artifacts.

#### Scenario: Public API changes incompatibly
- **WHEN** a release removes or changes a previously stable public contract without an approved major version
- **THEN** automated compatibility validation rejects the package

#### Scenario: Package is tested before promotion
- **WHEN** a prerelease package is produced
- **THEN** sample consumers restore from an isolated feed and execute their conformance tests using the package rather than project references

#### Scenario: Release evidence is incomplete
- **WHEN** live collector, failure-injection, package-integrity, supported-runtime, or performance checks are skipped
- **THEN** release evidence reports them as unverified and the stable release is not described as fully production ready

### Requirement: Migration preserves consumer behavior
The extraction SHALL provide a documented migration path from embedded observability code to package consumption. A consumer SHALL be able to compare old and packaged implementations using the same contract fixtures, and migration SHALL not remove the embedded implementation until the packed prerelease demonstrates equivalent public configuration, signal schemas, privacy controls, failure isolation, lifecycle bounds, and application behavior. Rollback SHALL be possible by pinning the last known-good package and application release without changing telemetry backends or public request contracts.

#### Scenario: AgentMemoryMCP migrates to the package
- **WHEN** AgentMemoryMCP replaces its local reusable observability infrastructure with published prerelease packages
- **THEN** its existing MCP observability conformance suite and published-binary smoke tests pass without tool-schema, stdout, telemetry-schema, or storage-behavior regression

#### Scenario: Parity check fails
- **WHEN** packaged behavior changes an existing signal, privacy rule, shutdown bound, or MCP response
- **THEN** local implementation removal and stable package promotion are blocked with the mismatch recorded

#### Scenario: Consumer rolls back
- **WHEN** an adopted package version causes an operational regression
- **THEN** operators can restore the documented last known-good package/application version while retaining compatible configuration and collector deployment
