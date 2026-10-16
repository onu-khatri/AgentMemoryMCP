# Supporting Information: Standalone MCP Server Architecture

**Story:** ARCH-MCP-001
**Source requirement:** `UserStory/References/standalone-mcp-server-architecture-requirements-dotnet10.md`
**Delivery shape:** Architecture / backend platform

## Affected Boundaries

The target shape is a standalone solution with a composition-root Host, thin Protocol inbound adapters, infrastructure-independent Core abstractions, independently understandable Modules, shared Infrastructure, Service Defaults, deployment assets, and separate test layers. Exact project names may vary, but the dependency direction is a contract.

Expected direction:

```text
Consumers -> MCP protocol -> Host/Protocol -> Core <- Modules/Infrastructure
```

Consumers must not reference server projects directly. Core must not reference transport or implementation details.

## Capability Flow

```text
Client request
  -> authentication and request context
  -> authorization policy/filter
  -> protocol schema validation
  -> narrow MCP tool/resource/prompt
  -> Core capability abstraction
  -> module or infrastructure adapter
  -> normalized result/error
  -> logs, metrics, and trace completion
```

`Program.cs` remains a small composition root: configuration, authentication, authorization, module registration, MCP registration, middleware, health/default endpoints, MCP mapping, and host startup.

## State And Error Matrix

| Condition | Required behavior | Verification focus |
|---|---|---|
| Valid short read | Execute through Core and return a bounded result | Unit, MCP contract, integration |
| Invalid or oversized input | Reject before protected/dependency operation | Validation and contract tests |
| Unauthorized/forbidden | Return stable auth response; do not disclose unavailable capability where policy requires | Authorization tests |
| Dependency timeout/unavailable | Apply configured timeout/resilience and return normalized dependency error | Integration and resilience tests |
| Cancellation | Propagate token through tool, Core, module, and dependency | Cancellation tests |
| Duplicate side-effect request | Apply capability-appropriate idempotency decision | Contract/integration tests |
| Long-running operation | Use durable task capability rather than holding a standard request indefinitely | Task integration/E2E tests |
| Invalid required configuration | Fail fast during startup | Host test or startup verification |
| Multiple replicas | Avoid process-local critical state and sticky-session dependency | Deployment/E2E verification |

## Contract Surfaces

- MCP tool/resource/prompt names, descriptions, schemas, authorization visibility, outputs, and stable errors.
- Core capability abstractions and request context containing client, tenant/user where applicable, scopes, and correlation ID.
- Module registration extensions and typed Options.
- Health/readiness endpoints and telemetry conventions.
- Architecture-test rules for forbidden references and dependency direction.

## Implementation Constraints

- Use explicit tool registration for the production public surface.
- Use typed `HttpClient` instances and standard resilience handlers for external HTTP.
- Keep feature-specific infrastructure inside its module when it is not genuinely shared.
- Avoid generic `Common`, `Shared`, or `Utils` dumping grounds and interfaces without a replaceable architectural seam.
- Do not manually duplicate authorization, retry, or infrastructure exception handling in each tool.
- Protect metric dimensions from high cardinality and redact secrets/tokens/sensitive payloads from logs.

## Test Focus

- Architecture tests enforce forbidden assembly references and consumer isolation.
- MCP contract tests verify tool registration, schemas, authorization, errors, cancellation, and compatibility.
- Unit tests cover Core policies, validation, mapping, and normalized errors.
- Integration tests cover selected external adapters, durable state, cache, authentication integration, and resilience.
- End-to-end tests exercise the real MCP boundary and selected multi-replica/stateless behavior.
- CI gates should include format, Release build, tests, security/dependency scan, container build, and applicable deployment checks.

## Open Implementation Decisions

- Which initial module and sample capability demonstrates the architecture?
- Which identity provider, token model, client registration, scopes, and tenant model apply?
- Which database/cache/task/queue/secret services are required for the first slice?
- Which MCP SDK version and exact stateless transport APIs are compatible with the chosen .NET 10 toolchain?
- What deployment platform, telemetry exporters, availability targets, and performance thresholds are required?
