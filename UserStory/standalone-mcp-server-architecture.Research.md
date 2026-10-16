# Research And Evidence: Standalone MCP Server Architecture

**Story:** ARCH-MCP-001
**Research date:** 2026-10-08

## Evidence Register

### Observed

1. `UserStory/References/standalone-mcp-server-architecture-requirements-dotnet10.md` explicitly requires an independent, standalone, deployable MCP service for multiple applications, AI agents, IDEs, and MCP-compatible clients.
2. The source requirement defines boundaries for Host, Protocol, Core, Modules, Infrastructure, tests, and deployment, including forbidden dependency directions.
3. The source requirement specifies .NET 10, ASP.NET Core, the official MCP C# SDK, Streamable HTTP, stateless sessions by default, built-in DI, validated Options, OpenTelemetry, typed HTTP clients, resilience, authentication/authorization, and optional Aspire defaults.
4. The source requirement explicitly requires input validation, cancellation propagation, stable error normalization, observability, architecture tests, MCP contract tests, independent deployment, health/readiness, and horizontal scaling.
5. The repository currently contains the existing application projects and the source requirement, but no `Business-Requirements/`, `User-Stories/`, or `UserStory/` story-pack artifacts were present when this pack was authored.

### Inferred

- This is an architecture/platform story rather than a user-facing feature story because its value is reduced coupling, independent operation, and safe reuse by multiple clients.
- The source is too broad for a single implementation slice without selecting an initial module and deployment target; the story therefore records those as Definition-of-Ready decisions and keeps status `Draft`.
- The most important early verification is boundary enforcement plus one demonstrable vertical capability through the real MCP protocol, because diagrams alone cannot prove the architecture.

### Recommended

- Start implementation with one representative read capability and its module/integration adapter, while establishing the shared Host, Protocol, Core, security, telemetry, and architecture-test seams.
- Resolve identity, durable state, deployment, package-version, and operational target decisions before marking the story ready for engineering.
- Treat tool schemas, authorization behavior, normalized errors, and cancellation as contract-tested public behavior from the first capability.

## External Research

No external research was needed to author this pack. The supplied repository requirement is the authoritative source for the requested architecture. SDK API details, current package versions, and deployment-specific choices remain implementation-time verification items rather than asserted facts in this story.

## Traceability

The `.US.md` states the outcome and observable acceptance criteria. The `.SI.md` translates the source architecture into implementation boundaries, state/error behavior, contract surfaces, and test focus. This file preserves the distinction between facts from the requirement, conclusions drawn from them, and recommendations awaiting approval.
