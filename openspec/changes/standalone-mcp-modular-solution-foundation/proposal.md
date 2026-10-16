## Why

The current repository concentrates hosting, MCP protocol adapters, capability orchestration, persistence, integrations, observability, and tests around one production project, which conflicts with the reference architecture's explicit requirement not to keep all code in one project. A real multi-project foundation is needed before adding more tools or modules so dependency direction, module ownership, independent deployment, and clean-code boundaries are enforced by the build graph rather than folder conventions.

## What Changes

- Restructure the .NET 10 solution into separate buildable projects for Host, Protocol, Core, shared Infrastructure, Service Defaults, Session module and contracts, AgentMemory module and contracts, and purpose-specific test suites.
- Establish `src/`, `tests/`, and `deploy/` roots plus `Directory.Build.props`, `Directory.Packages.props`, `global.json`, and the `.slnx` project graph.
- Make Core the infrastructure- and protocol-independent center for request context, errors/results, execution policies, security abstractions, and meaningful integration seams.
- Keep MCP adapters thin in Protocol and register the public tool surface explicitly; migrate existing tool names, schemas, behavior, cancellation, errors, and stdio compatibility without adding a new MCP tool or resource.
- Make Session and AgentMemory independently understandable vertical modules with their own contracts, implementation, dependency-registration boundary, module-specific infrastructure where appropriate, and focused tests.
- Keep shared Infrastructure limited to genuinely cross-module adapters such as canonical filesystem primitives, shared resilience, and observability integration; prevent it from becoming a generic dumping ground.
- Add an independently deployable ASP.NET Core Host using Streamable HTTP and stateless-by-default handling while retaining a bounded compatibility path for the existing stdio package during migration.
- Add validated Options, authentication/authorization composition, health/readiness, OpenTelemetry, explicit failure normalization, and end-to-end cancellation propagation without changing existing capability semantics.
- Replace the monolithic test project with Unit, Integration, MCP Contract, Architecture, and End-to-End projects that validate the real assembly/project dependency graph.
- Provide container and local orchestration assets required to run the standalone host independently and verify replica-safe use of canonical durable state.

## Capabilities

### New Capabilities

- `standalone-mcp-platform-foundation`: Defines the independently deployable, multi-project MCP platform structure, dependency rules, module contract, host composition, compatibility constraints, and architecture verification required for future modules and tools.

### Modified Capabilities

<!-- Existing session, memory, storage, and observability capability behavior is preserved. This change restructures ownership and deployment boundaries without changing their established requirements. -->

## Impact

- **Solution/build:** `AgentMemoryMCP.slnx`, project locations/references, central build properties, central package versions, SDK selection, packaging, and CI commands.
- **Runtime composition:** Existing `AgentSession.MCP` startup is decomposed into Host composition, Protocol adapters, Core contracts, module registrations, Infrastructure adapters, and Service Defaults.
- **Modules:** Existing session lifecycle/resume/checkpoint/task tools move to the Session module; reusable `memory_*` tools and semantic integrations move to AgentMemory. No new tool is introduced.
- **Compatibility:** Existing MCP tool names and schemas remain public contracts. Stdio compatibility is retained during migration while Streamable HTTP becomes the standalone deployment path.
- **Persistence:** Existing repository-scoped canonical filesystem records remain authoritative; this foundation does not introduce SQL, Redis, queues, a task store, or a new session format.
- **Quality and security:** Architecture tests, contract tests, cancellation checks, configuration validation, normalized failures, structured telemetry, authentication/authorization wiring, health/readiness, and container verification become first-class solution concerns.
- **Future growth:** New modules follow the same contract/implementation/registration/test pattern without direct module-to-module implementation references or consumer application project references.
