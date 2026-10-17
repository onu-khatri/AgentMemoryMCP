## Purpose

Establishes a standalone, independently deployable MCP platform whose modular project graph, stable Core, explicit protocol boundary, and isolated capability modules can support long-term growth without changing existing tool contracts.

## ADDED Requirements

### Requirement: Standalone service boundary

The MCP platform SHALL run and deploy independently from every consuming application. Consumers SHALL interact through MCP contracts only and SHALL NOT require direct project references to platform implementation projects or shared consumer business assemblies.

#### Scenario: Independent consumer connects

- **WHEN** an MCP-compatible application, agent, or IDE connects to the deployed platform
- **THEN** it can discover and invoke the authorized existing capabilities without loading or referencing platform implementation assemblies

#### Scenario: Solution references are inspected

- **WHEN** project references are evaluated by architecture verification
- **THEN** no consumer application project is referenced by the platform and no consumer directly references a platform implementation project

### Requirement: Enforced multi-project architecture

The solution SHALL separate Host, Protocol, Core, shared Infrastructure, Service Defaults, Session module, AgentMemory module, module contracts, and purpose-specific test concerns into distinct buildable projects. Folder placement alone SHALL NOT satisfy a project boundary. The solution SHALL use centralized build and package-version configuration.

#### Scenario: Solution graph is inspected

- **WHEN** the solution and project files are inspected
- **THEN** each required architectural boundary is represented by a distinct project with explicit references and centralized build/package configuration

#### Scenario: Boundary project is removed from the graph

- **WHEN** a required project is absent or no longer participates in the solution build
- **THEN** architecture or build verification fails rather than treating a folder marker as equivalent evidence

### Requirement: Stable infrastructure-independent Core

Core SHALL own stable request context, execution contracts, common error/result contracts, policies, security abstractions, and meaningful integration seams. Core SHALL NOT reference Host, Protocol, ASP.NET Core, MCP transport types, persistence implementations, provider SDKs, module implementations, or consumer applications.

#### Scenario: Core dependency rule is evaluated

- **WHEN** architecture tests inspect Core assembly and project references
- **THEN** only approved platform-neutral dependencies are present and every prohibited framework, transport, provider, module-implementation, and consumer dependency is rejected

#### Scenario: Capability needs an external service

- **WHEN** capability behavior requires storage or an external provider
- **THEN** Core exposes only a meaningful abstraction or policy seam and the concrete adapter remains outside Core

### Requirement: Thin and explicit MCP protocol boundary

Protocol SHALL act as an inbound MCP adapter responsible for tools, resources, prompts, MCP schemas, filters, protocol validation, mapping, and stable protocol errors. Protocol adapters SHALL delegate capability execution through stable contracts and SHALL NOT directly perform persistence, cache access, provider HTTP calls, retries, authentication logic, complex orchestration, or infrastructure exception handling.

#### Scenario: Existing tool is invoked

- **WHEN** a client invokes an existing MCP tool
- **THEN** the Protocol adapter validates and maps the request, delegates once through the owning capability contract, and maps the result or normalized failure without implementing the capability rule itself

#### Scenario: Protocol references are inspected

- **WHEN** architecture tests inspect Protocol references
- **THEN** concrete persistence, cache, provider SDK, and module implementation references are rejected

### Requirement: Independently understandable vertical modules

Each capability module SHALL have explicit contracts, implementation, registration, external dependencies, authorization requirements, failure modes, observability responsibilities, and focused tests. Session SHALL own session lifecycle, resume, checkpoint, task coordination, and session-specific `append_agent_memory`; AgentMemory SHALL own reusable cross-session `memory_*` behavior and semantic integrations. Modules SHALL NOT depend on another module's implementation.

#### Scenario: Session ownership is inspected

- **WHEN** the Session module and its registration are reviewed
- **THEN** session-owned behavior and contracts are cohesive, independently testable, and free of concrete AgentMemory implementation dependencies

#### Scenario: AgentMemory ownership is inspected

- **WHEN** the AgentMemory module and its registration are reviewed
- **THEN** reusable memory behavior, provider adapters specific to that module, and its contracts are cohesive and free of concrete Session implementation dependencies

#### Scenario: Cross-module collaboration is required

- **WHEN** one module needs behavior owned by another
- **THEN** collaboration crosses an explicitly approved Core or contracts boundary and architecture verification rejects direct implementation coupling

### Requirement: Explicit composition and public capability registration

Host SHALL be the only composition root and SHALL explicitly register Core, Infrastructure, modules, authentication, authorization, observability, health, transport, and the public MCP capability surface. The refactor SHALL NOT add, remove, rename, or silently change an existing MCP tool, resource, prompt, input schema, output schema, or semantic contract.

#### Scenario: Host composition is reviewed

- **WHEN** startup composition is inspected
- **THEN** Host contains deployment wiring only, uses explicit module and tool registration, and contains no capability, persistence, or integration logic

#### Scenario: MCP contract compatibility is verified

- **WHEN** pre-refactor and post-refactor tool discovery and contract snapshots are compared
- **THEN** existing names, descriptions, schemas, authorization metadata, and observable semantics remain compatible

### Requirement: Stateless multi-client hosting and compatibility

The standalone ASP.NET Core deployment SHALL expose Streamable HTTP with stateless request handling by default, identify client, user, tenant when applicable, scopes, and correlation context without coupling to a consumer, and keep critical state outside process memory. The migration SHALL retain a bounded stdio compatibility path until its existing consumers can move without a breaking change.

#### Scenario: Requests reach different replicas

- **WHEN** successive requests for the same repository are served by different healthy platform replicas
- **THEN** they use the same authoritative durable state without sticky-session affinity or process-local critical state

#### Scenario: Existing stdio consumer runs during migration

- **WHEN** an existing compatible stdio consumer starts the packaged server
- **THEN** its established tool contracts remain available while the standalone HTTP host is introduced separately

### Requirement: Secure validated operational foundation

The platform SHALL use validated strongly typed configuration, centralized standards-based authentication and policy/scope authorization, end-to-end cancellation, bounded inputs and outputs, normalized failures, structured correlated logging, OpenTelemetry traces and metrics, dependency-aware health/readiness, semantically safe resilience, and capacity controls. Secrets, credentials, sensitive payloads, raw infrastructure exceptions, and unbounded high-cardinality values SHALL NOT be exposed.

#### Scenario: Required configuration is invalid

- **WHEN** a required host, storage, authentication, or integration setting is missing or unsafe
- **THEN** startup fails before accepting MCP traffic with actionable non-secret diagnostics

#### Scenario: Request is cancelled

- **WHEN** a client cancellation reaches a tool invocation
- **THEN** cancellation flows through Protocol, capability, module, and adapter operations and the platform does not report a successful completion

#### Scenario: Dependency fails

- **WHEN** storage or an external integration times out or becomes unavailable
- **THEN** the owning module applies only semantically safe resilience, unrelated modules remain available, and the client receives a stable normalized failure without implementation details

### Requirement: Canonical state compatibility

The restructuring SHALL preserve the repository-scoped canonical filesystem records, path safety, managed transaction and locking behavior, checkpoints, snapshots, cursors, and existing recovery semantics. It SHALL NOT introduce a database, distributed cache, queue, durable task store, or a new session/memory record format as part of this foundation.

#### Scenario: Existing repository data is opened after migration

- **WHEN** the restructured platform starts against an existing canonical storage root
- **THEN** existing session and memory records remain readable and writable without a data migration or semantic reinterpretation

#### Scenario: Repository boundary is challenged

- **WHEN** an operation attempts to escape or cross its configured repository boundary
- **THEN** the platform rejects it without disclosing or mutating another repository's state

### Requirement: Layered verification and delivery gates

The solution SHALL provide separate Unit, Integration, MCP Contract, Architecture, and End-to-End test projects. CI SHALL perform deterministic restore, formatting, Release build, unit tests, architecture tests, integration tests, contract tests, security/dependency scanning, container build, and applicable end-to-end checks before publication or deployment.

#### Scenario: Architecture violation is introduced

- **WHEN** Core, Protocol, Host, a module, Infrastructure, or a consumer violates the approved project dependency rules
- **THEN** the dedicated architecture test project fails in local verification and CI

#### Scenario: Containerized host is verified

- **WHEN** deployment verification starts the standalone host with external configuration and canonical storage
- **THEN** health/readiness, graceful shutdown, OpenTelemetry wiring, MCP discovery, and an authorized existing tool flow are independently verifiable

#### Scenario: New module is proposed later

- **WHEN** a future module is added to the platform
- **THEN** it follows the established contracts/implementation/registration/test pattern and does not require weakening Core, Protocol, or module-independence rules
