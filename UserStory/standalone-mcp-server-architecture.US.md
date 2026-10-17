---
id: ARCH-MCP-001
title: Establish a standalone, deployable MCP platform
status: In_Progress
branch: gh-1-standalone-mcp-modular-solution-foundation
worktree_path: .worktrees/standalone-mcp-modular-solution-foundation
base_branch: main
pr_url:
is_architectural: true
approach_summary: Establish an independently deployable .NET 10 MCP service with thin protocol adapters, a stable Core, modular integrations, and enforceable operational boundaries.
created: 2026-10-08
updated: 2026-10-11
---

## GitHub Issues

| GitHub issue | Created date | Number |
| --- | --- | --- |
| [Establish a standalone, deployable MCP platform](https://github.com/onu-khatri/AgentMemoryMCP/issues/1) | 2026-10-11 | 1 |

## [ARCH-MCP-001] - Establish a standalone, deployable MCP platform

**Module:** MCP platform architecture
**Layer:** Architecture
**Priority:** Not specified in source requirements
**Persona:** Platform engineer and MCP consumer-integration team
**Depends on:** None identified; implementation decisions remain prerequisites
**Source Classification:** Observed

### User Story

As a platform engineer, I want an independently deployable MCP server with stable architectural boundaries, so that multiple applications, agents, IDEs, and other MCP clients can consume shared capabilities without direct application coupling.

### Description

The MCP server is a standalone platform/service, not a layer inside a consuming application. It must expose capabilities through the MCP protocol while keeping protocol handling, Core capability orchestration, integrations, infrastructure, and consuming applications independently bounded.

### Scope

In scope:

- .NET 10 ASP.NET Core host using Streamable HTTP and stateless sessions by default.
- Host, Protocol, Core, Modules, Infrastructure, tests, and deployment boundaries described by the source requirement.
- Standards-based authentication and policy/scope/tool authorization.
- Validated Options, typed HTTP clients, resilience, cancellation propagation, structured logging, OpenTelemetry, and health endpoints.
- Explicit MCP tool registration and contract, architecture, unit, integration, and end-to-end test layers.
- Container-ready independent deployment with multiple replicas and no process-local critical state.

Out of scope:

- Implementing a specific consumer application or its business logic in the MCP server.
- Selecting concrete external providers, databases, queues, secret managers, or cloud services beyond the required seams.
- Creating a separate microservice for every module.
- Defining product-specific tools, entitlement rules, success targets, or migration plans not present in the source requirement.

### Preconditions

- The implementation team confirms the initial module/capability slice and deployment environment.
- The versions of the MCP C# SDK and supporting packages are selected and compatible with .NET 10.
- Authentication authority, authorization policy ownership, and required client/tenant context are defined.
- Required durable stores for distributed state and long-running tasks are selected where applicable.

### Detailed Workflow

1. An independent MCP-compatible client connects to the standalone host through the MCP protocol.
2. The host authenticates the request, establishes request context, and applies authorization policies.
3. The protocol adapter validates the request and invokes a narrow, explicitly registered tool/resource/prompt.
4. The tool delegates to a Core capability abstraction.
5. A module or infrastructure adapter performs the operation with cancellation, timeout, resilience, and observability applied.
6. The server returns a stable MCP result or normalized error without exposing infrastructure exceptions.
7. Logs, metrics, and traces correlate the MCP request with the capability and downstream dependency activity.

### Functional Requirements

- The server MUST be independently buildable, deployable, and horizontally scalable.
- Consumers MUST communicate through MCP only; consumer projects MUST NOT be direct project references of the server.
- Core MUST remain independent of the host, MCP transport, infrastructure implementations, consumer applications, and provider SDKs.
- Protocol tools MUST be thin and MUST NOT contain direct database/cache/HTTP access, authentication logic, retry logic, or substantial business orchestration.
- Modules MAY implement Core abstractions and MUST declare their capabilities, dependencies, authorization, failure modes, observability, and tests.
- The server MUST use stateless Streamable HTTP sessions by default and MUST keep critical shared state outside process memory.
- Externally callable capabilities MUST validate untrusted input, enforce authorization, honor cancellation, and constrain dangerous or expensive operations.
- External HTTP integrations MUST use typed clients from `IHttpClientFactory` and semantically safe resilience policies.
- Configuration MUST use strongly typed Options with startup validation for required settings.
- Observability MUST include structured logs, OpenTelemetry traces and metrics, correlation, and safe cardinality choices.
- External errors MUST be normalized into stable categories such as validation, unauthorized, forbidden, not found, conflict, rate limited, dependency unavailable, timeout, cancelled, and unexpected.
- Architecture rules MUST be enforced by automated tests, and MCP schemas/tool names MUST be covered as public contracts.

### Acceptance Criteria

1. **Standalone boundary** — Given a consumer application and the MCP server, when the solution is inspected and built, then the consumer has no direct project reference or shared business-layer assembly dependency on the MCP server, and the server runs as its own deployable service.
2. **Layer direction** — Given the solution dependency graph, when architecture tests run, then Core does not reference Host, Protocol, infrastructure implementations, provider SDKs, or consumer applications; Protocol does not directly access infrastructure implementations; and uncontrolled module-to-module dependencies are rejected.
3. **MCP transport** — Given a valid MCP client request, when it reaches the server, then Streamable HTTP handles it with stateless sessions by default and the request is routed through an explicitly registered, narrow protocol capability.
4. **Security and input safety** — Given unauthenticated, unauthorized, malformed, oversized, or dangerous input, when a capability is called, then the server rejects it with an appropriate stable response and does not perform the protected operation.
5. **Delegation and cancellation** — Given a valid capability request, when the operation executes or is cancelled, then the protocol layer delegates through a Core abstraction, downstream integrations receive the cancellation token, and the response does not expose raw infrastructure exceptions.
6. **Operational resilience** — Given a dependency timeout, transient failure, rate limit, or unavailable integration, when the capability handles the failure, then configured timeout/resilience and normalized error behavior apply without unnecessarily destabilizing unrelated modules.
7. **Configuration and observability** — Given invalid required configuration or a normal request, when the host starts or processes the request, then invalid configuration fails fast, and logs/traces/metrics provide safe correlation across MCP, tool, Core, and dependency activity.
8. **Verification and deployment** — Given a release build, when the required pipeline runs, then formatting, build, unit, integration, MCP contract, architecture, security/dependency, container, and applicable end-to-end checks are available as defined gates, and the service supports health/readiness checks, graceful shutdown, and multiple replicas without process-local critical state.

### Business Rules

- The MCP server is a shared platform boundary, not a consuming application layer.
- Reads and writes have explicit semantics; mutation operations require appropriate idempotency, auditing, transaction, and concurrency decisions.
- One client, tenant, or tool must not be able to unintentionally exhaust shared capacity.
- Tool names and input/output schemas are public contracts and should evolve additively where possible.
- Any design decision that violates these constraints requires explicit documented justification.

### Validation Rules

- Validate required fields, bounds, maximum lengths, collection sizes, query constraints, path traversal risks, injection risks, and response-size limits as applicable to each capability.
- Validate required Options at startup.
- Validate authorization at policy/capability boundaries rather than duplicating ad hoc checks in every tool.

### Error Handling

Return stable protocol-facing categories for validation, authentication/authorization, not found, conflict, rate limiting, dependency failure, timeout, cancellation, and unexpected failures. Do not expose secrets, credentials, sensitive payloads, or raw infrastructure exceptions.

### Security Considerations

Use least privilege, standards-based authentication, policy/scope/tool authorization, secure secret management, audit logging for sensitive operations, rate limiting, SSRF/path traversal/injection protections, dependency scanning, and safe defaults. Do not expose arbitrary shell execution, SQL, filesystem access, or unrestricted outbound HTTP without an explicitly justified requirement.

### Analytics / Tracking

No product analytics requirement is defined. Operational telemetry is required for tool calls, failures, duration, timeouts, cancellations, resource reads, prompt requests, active requests, and integration activity, using bounded dimensions such as tool, module, status, integration, and client type.

### API Dependencies

- Official MCP C# SDK
- Streamable HTTP transport
- ASP.NET Core authentication and authorization
- OpenTelemetry
- `IHttpClientFactory`
- Standard .NET resilience mechanisms
- Optional Aspire service defaults/orchestration where useful

### Data Requirements

Critical shared state must use appropriate durable or distributed storage. Redis/cache, SQL/PostgreSQL, object storage, durable task storage, queues, distributed coordination, and secret managers are implementation options subject to the selected capability. No critical state may depend on process memory or sticky sessions by default.

### UX Notes

The primary experience is the MCP client contract rather than a human UI. Tool discovery/listing must respect authorization where appropriate, and tool names/descriptions/schemas must be explicit, narrow, predictable, and independently testable.

### Definition Of Ready For Engineering

- Initial module and capability slice is selected.
- Deployment target and runtime package versions are confirmed.
- Authentication authority, client/tenant context, authorization policies, and secret-management approach are defined.
- Durable state/task requirements are identified.
- Required architecture, MCP contract, and integration test infrastructure is available or explicitly planned.
- Any deviation from this source requirement has an approved architecture decision.

### Definition of Done

- The standalone .NET 10 MCP host builds and runs independently.
- Streamable HTTP, stateless-by-default behavior, health endpoints, authentication/authorization wiring, validated Options, structured logging, and OpenTelemetry are demonstrated.
- At least one sample module calls a Core abstraction and an integration implements it.
- Architecture, unit, integration, MCP contract, and end-to-end tests cover the implemented slice.
- Cancellation, normalized errors, resilience, and security validation are demonstrated for applicable paths.
- Release/container checks and multi-replica/stateless deployment behavior are documented and verified for the selected environment.

### Related Documents

- [Standalone MCP Server Architecture Requirements](References/standalone-mcp-server-architecture-requirements-dotnet10.md)
- [Supporting implementation information](standalone-mcp-server-architecture.SI.md)
- [Repository and requirement evidence](standalone-mcp-server-architecture.Research.md)
