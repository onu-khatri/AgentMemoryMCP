# Standalone MCP Server Architecture Requirements (.NET 10)

## 1. Purpose

Build a production-grade **Model Context Protocol (MCP) Server** using **.NET 10**.

The MCP Server must be an **independent, standalone, deployable service** that can be consumed by multiple applications, AI agents, IDEs, and other MCP-compatible clients.

The MCP Server must **not** be embedded into, coupled to, or treated as a layer of any consuming application.

---

## 2. Core Architectural Principle

The system boundary is:

```text
Application A ─┐
Application B ─┼──► MCP Server ───► External systems / services
Application C ─┤
AI Agents     ─┤
IDEs          ─┘
```

Not:

```text
Application
├── Domain
├── Application
├── Infrastructure
└── MCP
```

The MCP Server is its own platform/service.

Consumer applications communicate with it **only through the MCP protocol**.

There must be:

- No direct project references from consumer applications to the MCP Server.
- No shared application/business-layer assemblies between consumers and the MCP Server.
- No assumptions in the MCP Server about a specific consuming application.
- No application-specific business logic embedded into MCP protocol handlers.

---

## 3. Primary Goals

The architecture must optimize for:

- Maintainability
- Scalability
- Robustness
- SOLID principles
- Clear dependency boundaries
- Independent deployment
- Horizontal scaling
- Security
- Observability
- Testability
- Extensibility
- Multi-client usage
- Long-term evolution
- Modular growth to dozens or hundreds of MCP tools

---

## 4. Technology Baseline

Use:

- .NET 10
- ASP.NET Core
- Official Model Context Protocol C# SDK
- Streamable HTTP transport
- Stateless MCP sessions by default
- Built-in Microsoft Dependency Injection
- Strongly typed Options configuration
- OpenTelemetry
- `IHttpClientFactory`
- Standard .NET resilience mechanisms
- ASP.NET Core authentication and authorization
- Aspire Service Defaults where useful
- `.slnx`
- `Directory.Build.props`
- `Directory.Packages.props`

Avoid unnecessary framework dependencies.

---

## 5. Recommended Solution Structure

```text
McpPlatform/
│
├── src/
│   │
│   ├── McpPlatform.Host/
│   │   ├── Program.cs
│   │   ├── Configuration/
│   │   ├── Authentication/
│   │   ├── Middleware/
│   │   └── DependencyInjection.cs
│   │
│   ├── McpPlatform.Core/
│   │   ├── Abstractions/
│   │   ├── Context/
│   │   ├── Errors/
│   │   ├── Execution/
│   │   ├── Policies/
│   │   └── Security/
│   │
│   ├── McpPlatform.Protocol/
│   │   ├── Tools/
│   │   ├── Resources/
│   │   ├── Prompts/
│   │   ├── Filters/
│   │   ├── Contracts/
│   │   └── DependencyInjection.cs
│   │
│   ├── Modules/
│   │   ├── GitHub/
│   │   │   ├── McpPlatform.GitHub/
│   │   │   └── McpPlatform.GitHub.Contracts/
│   │   │
│   │   ├── Jira/
│   │   │   ├── McpPlatform.Jira/
│   │   │   └── McpPlatform.Jira.Contracts/
│   │   │
│   │   ├── Knowledge/
│   │   │   ├── McpPlatform.Knowledge/
│   │   │   └── McpPlatform.Knowledge.Contracts/
│   │   │
│   │   └── Database/
│   │       ├── McpPlatform.Database/
│   │       └── McpPlatform.Database.Contracts/
│   │
│   ├── McpPlatform.Infrastructure/
│   │   ├── Persistence/
│   │   ├── Caching/
│   │   ├── Messaging/
│   │   ├── Tasks/
│   │   ├── Resilience/
│   │   └── Observability/
│   │
│   └── McpPlatform.ServiceDefaults/
│
├── tests/
│   ├── McpPlatform.UnitTests/
│   ├── McpPlatform.IntegrationTests/
│   ├── McpPlatform.ContractTests/
│   ├── McpPlatform.ArchitectureTests/
│   └── McpPlatform.EndToEndTests/
│
├── deploy/
│   ├── McpPlatform.AppHost/
│   ├── Dockerfile
│   └── ...
│
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── McpPlatform.slnx
└── README.md
```

The exact names may change, but the architectural boundaries must remain.

---

## 6. Layer Responsibilities

### 6.1 McpPlatform.Host

Responsibility:

- Process startup
- ASP.NET Core hosting
- Authentication configuration
- Authorization configuration
- Dependency injection composition
- Configuration loading
- Health endpoints
- MCP endpoint mapping
- Service-default registration
- Deployment-specific wiring

The Host is the **composition root**.

`Program.cs` must stay small.

It must not contain business/integration logic.

---

### 6.2 McpPlatform.Protocol

Responsibility:

- MCP Tools
- MCP Resources
- MCP Prompts
- MCP-specific contracts
- MCP input/output mapping
- MCP filters
- Protocol validation
- Protocol-level error mapping

This layer is an **inbound adapter**.

MCP tools should be thin.

Example:

```csharp
[McpServerToolType]
public sealed class RepositoryTools(
    IRepositorySearchService searchService)
{
    [McpServerTool(Name = "repository_search")]
    public Task<SearchRepositoryResponse> SearchAsync(
        SearchRepositoryRequest request,
        CancellationToken cancellationToken)
    {
        return searchService.SearchAsync(
            request,
            cancellationToken);
    }
}
```

MCP tools must not directly contain:

- Database access
- Redis access
- Raw HTTP calls
- Complex orchestration
- Retry implementation
- Authentication logic
- Authorization logic
- Infrastructure-specific exception handling
- Large amounts of domain/business logic

---

### 6.3 McpPlatform.Core

The Core is the stable center of the MCP platform.

Responsibility:

- Capability abstractions
- Execution/orchestration
- Request context
- Policies
- Common errors/results
- Security abstractions
- Integration abstractions
- Cross-cutting contracts

Core must remain infrastructure-independent.

Core must not depend on:

- ASP.NET Core
- MCP transport
- SQL implementation
- Redis implementation
- Specific third-party SDKs
- GitHub SDK
- Jira SDK
- Cloud-provider SDKs
- Consumer applications

Example abstraction:

```csharp
public interface IRepositorySearchService
{
    Task<SearchRepositoryResponse> SearchAsync(
        SearchRepositoryRequest request,
        CancellationToken cancellationToken);
}
```

---

### 6.4 Modules / Integrations

Modules represent capabilities exposed by the MCP platform.

Examples:

- GitHub
- GitLab
- Azure DevOps
- Jira
- Confluence
- Knowledge search
- Databases
- Object storage
- Internal APIs
- Search
- Document processing

A module may implement Core abstractions.

Example:

```text
IRepositoryProvider
├── GitHubRepositoryProvider
├── AzureDevOpsRepositoryProvider
└── GitLabRepositoryProvider
```

MCP tools should depend on stable Core abstractions instead of concrete providers.

---

### 6.5 Infrastructure

Responsibility:

- Persistence
- Cache
- Redis
- Queues
- Durable tasks
- Distributed coordination
- Observability exporters
- Shared resilience infrastructure
- Common storage implementation

Infrastructure must not become a dumping ground.

Prefer feature-specific infrastructure inside modules when it is specific to only one module.

---

## 7. Dependency Direction

Use this dependency direction:

```text
                    McpPlatform.Host
                          │
                          ▼
                 McpPlatform.Protocol
                          │
                          ▼
                   McpPlatform.Core
                          ▲
                          │
          ┌───────────────┴────────────────┐
          │                                │
          ▼                                ▼
       Modules                     Infrastructure
```

Hard rules:

```text
Core
    MUST NOT depend on
    Host
    Protocol
    Infrastructure implementations
    Third-party integration SDKs

Protocol
    MUST depend on abstractions
    MUST NOT directly depend on database/cache/provider implementations

Modules
    MAY implement Core abstractions

Host
    MAY compose all layers

Consumer Applications
    MUST NOT reference MCP Server projects directly
```

These rules must be enforced with architecture tests.

---

## 8. MCP Server Must Be Stateless by Default

Use Streamable HTTP with stateless session mode unless a feature explicitly requires stateful MCP behavior.

Architecture:

```text
                         Gateway

                            │
             ┌──────────────┼──────────────┐
             │              │              │
             ▼              ▼              ▼
       MCP Instance 1  MCP Instance 2  MCP Instance 3
             │              │              │
             └──────────────┼──────────────┘
                            │
              ┌─────────────┼─────────────┐
              ▼             ▼             ▼
            Redis         Database      Queue
```

Do not store critical state in process memory.

Avoid mutable static state.

Shared state must use appropriate durable or distributed storage.

Examples:

```text
Transient distributed cache → Redis
Business/platform state      → SQL/PostgreSQL
Files                        → Object storage
Long-running operations      → Durable task store
Messages                     → Queue / broker
Distributed locking          → Distributed coordination mechanism
Secrets                      → Secret manager
```

---

## 9. Multi-Client Request Context

The MCP Server may serve many applications and agents.

Introduce a first-class request context.

Example:

```csharp
public sealed record McpRequestContext(
    string ClientId,
    string? TenantId,
    string? UserId,
    IReadOnlySet<string> Scopes,
    string CorrelationId);
```

The exact contract may evolve.

It must make it possible to identify:

- Calling application/client
- Tenant
- User when applicable
- Granted scopes
- Correlation/trace ID

Do not couple this context to any specific application.

---

## 10. Authentication and Authorization

The server must support centralized authentication and authorization.

Prefer standards-based mechanisms such as:

- OAuth/OIDC
- JWT bearer authentication
- Client credentials for machine-to-machine scenarios
- Scope/policy-based authorization

Example authorization model:

```text
Application A
    repository_search
    document_read

Application B
    repository_search
    document_read
    ticket_create

Admin client
    explicitly granted administrative capabilities
```

Authorization must be policy-driven.

Avoid authorization checks manually duplicated in every MCP tool.

MCP listing behavior should respect authorization where appropriate so clients do not receive capabilities they cannot use.

---

## 11. Tool Design Rules

Tools must represent clear, bounded capabilities.

Prefer:

```text
repository_search
repository_get_file

document_search
document_read
document_upload
document_delete

ticket_get
ticket_create
ticket_update
```

Avoid:

```text
manage_repository
process_document
handle_ticket
database_operation
do_action
```

Tools should be:

- Explicit
- Narrow
- Predictable
- Independently authorizable
- Independently observable
- Independently testable
- Safely versionable

---

## 12. Input Is Untrusted

Assume MCP inputs may be generated by an LLM.

Every externally callable capability must consider:

- Schema validation
- Required fields
- Bounds
- Maximum lengths
- Maximum collection sizes
- Authorization
- Business/capability validation
- Cancellation
- Timeouts
- Maximum response size
- Injection risks
- Path traversal where applicable
- Query constraints
- Dangerous operation confirmation/authorization where applicable

Do not trust client-generated parameters.

---

## 13. Read and Write Operations

Keep read and write semantics explicit.

Conceptually:

```text
Queries
    read only

Commands
    may mutate state
```

Reads may support:

- Caching
- Safe retries
- Pagination

Writes may require:

- Idempotency
- Auditing
- Transactions
- Concurrency control
- Restricted retry behavior

Do not blindly retry mutation operations.

---

## 14. Idempotency

Operations with external side effects should support idempotency where appropriate.

Examples:

- Ticket creation
- Message/email sending
- Order submission
- File creation
- Provisioning
- Destructive operations

Concept:

```text
MCP request
    │
    ▼
Idempotency check
    │
    ├── Already processed → return previous result
    │
    └── New request
            │
            ▼
        Execute operation
            │
            ▼
        Store result
```

---

## 15. Long-Running Operations

Long-running workloads must not rely on holding a standard HTTP request open indefinitely.

Examples:

- Repository-wide analysis
- Large document indexing
- Bulk imports
- Large reports
- Migration jobs
- Long-running searches
- Expensive processing

Use MCP Task capabilities when appropriate.

Production task state should be durable and compatible with multiple server instances.

Concept:

```text
MCP tools/call
      │
      ▼
Capability service
      │
      ├── Short operation → Result
      │
      └── Long operation
               │
               ▼
          Durable task
               │
               ▼
        Background worker
               │
               ▼
           Task store
```

---

## 16. Cancellation

Cancellation tokens must flow end-to-end.

```text
MCP
 ↓
Protocol Tool
 ↓
Core Capability
 ↓
Integration
 ↓
HttpClient / Database / Storage
```

Do not drop `CancellationToken`.

---

## 17. External HTTP Integrations

Use:

```text
IHttpClientFactory
Typed clients
Standard resilience handlers
Timeouts
Circuit breakers where appropriate
Retries only when semantically safe
```

Do not instantiate ad-hoc `HttpClient` objects inside MCP tools.

Example:

```text
RepositoryTool
      ↓
RepositorySearchService
      ↓
IRepositoryProvider
      ↓
GitHubRepositoryProvider
      ↓
Typed HttpClient
      ↓
GitHub API
```

---

## 18. Configuration

Use strongly typed Options.

Example:

```csharp
public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public required Uri BaseUrl { get; init; }

    public int TimeoutSeconds { get; init; }
}
```

Validate configuration at startup.

Prefer fail-fast behavior for invalid required configuration.

Avoid scattering configuration key strings throughout the codebase.

---

## 19. Observability

Observability is mandatory.

Use OpenTelemetry for:

- Logs
- Metrics
- Distributed tracing

Recommended MCP-specific metrics:

```text
mcp.tool.calls
mcp.tool.failures
mcp.tool.duration
mcp.tool.timeout
mcp.tool.cancelled

mcp.resource.reads
mcp.prompt.requests

mcp.active_requests
mcp.integration.duration
mcp.integration.failures
```

Useful dimensions:

```text
tool.name
module
status
integration
client.type
```

Be careful with high-cardinality dimensions.

Avoid using values such as raw request ID, arbitrary user ID, raw query text, etc. as metric dimensions.

Tracing should make this chain visible:

```text
MCP Request
    │
    └── Tool
          │
          └── Core Capability
                  │
                  ├── Database
                  ├── Redis
                  └── External API
```

---

## 20. Logging

Use structured logging.

Every request should be correlatable.

Relevant context may include:

- Correlation ID
- Trace ID
- Tool name
- Module
- Client ID where safe
- Tenant ID where safe
- Duration
- Result category
- Dependency name

Do not log:

- Secrets
- Tokens
- Passwords
- Raw credentials
- Sensitive payloads unnecessarily

---

## 21. Error Model

Do not expose raw infrastructure exceptions as the external protocol contract.

Normalize errors into stable categories such as:

```text
ValidationError
Unauthorized
Forbidden
NotFound
Conflict
RateLimited
DependencyUnavailable
Timeout
Cancelled
Unexpected
```

Examples:

```text
DbUpdateConcurrencyException
        ↓
Conflict
        ↓
Stable MCP error
```

```text
HttpRequestException
        ↓
DependencyUnavailable
        ↓
Stable MCP error
```

Infrastructure technology must remain an implementation detail.

---

## 22. Resilience and Failure Isolation

The architecture must expect dependencies to fail.

Support:

- Timeouts
- Circuit breaking where appropriate
- Safe retry policies
- Bulkhead/concurrency controls if required
- Graceful degradation
- Dependency-specific health information
- Cancellation
- Rate limiting

One failing integration should not unnecessarily destabilize unrelated modules.

---

## 23. Rate Limiting

Rate limiting should be capable of accounting for:

```text
client_id
tenant_id
tool_name
```

One consuming application must not be able to unintentionally exhaust resources for every other consumer.

Consider separate policies for:

- Cheap read tools
- Expensive searches
- Write operations
- Long-running jobs
- Administrative tools

---

## 24. SOLID Guidance

Apply SOLID to architectural boundaries, not mechanically to every class.

Good abstractions:

```text
IRepositoryProvider
IDocumentStore
ITaskStore
ICache
IClock
IMessagePublisher
```

Do not automatically create interfaces for trivial classes without a real abstraction or replaceable boundary.

Avoid unnecessary patterns such as:

```text
ICustomerMapper -> CustomerMapper
ISimpleValidator -> SimpleValidator
ISimpleFactory -> SimpleFactory
```

unless there is a concrete architectural/testability reason.

SOLID should reduce coupling, not increase ceremony.

---

## 25. Shared Code

Avoid generic dumping grounds:

```text
Shared/
Common/
Helpers/
Utils/
```

Shared building blocks must remain minimal and stable.

Examples of reasonable shared concepts:

```text
Result
Error
Correlation context
Clock abstraction
Execution metadata
```

Module-specific code must remain inside its module.

---

## 26. Dependency Injection

Use the built-in Microsoft DI container unless a concrete requirement demands otherwise.

Prefer module registration extensions:

```csharp
services
    .AddMcpCore()
    .AddGitHubModule(configuration)
    .AddJiraModule(configuration)
    .AddKnowledgeModule(configuration);
```

Keep the composition root explicit.

Avoid service-locator patterns.

Avoid resolving arbitrary services manually from `IServiceProvider` in business/capability code.

---

## 27. MCP Tool Registration

Prefer explicit tool registration for large production systems.

Example:

```csharp
services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<RepositoryTools>()
    .WithTools<DocumentTools>()
    .WithTools<TicketTools>();
```

Benefits:

- Explicit public surface
- Better reviewability
- Safer feature exposure
- Easier authorization review
- Easier AOT compatibility
- Less accidental registration

Assembly scanning may be acceptable for small systems but should not become uncontrolled.

---

## 28. Example Host Composition

Keep `Program.cs` approximately this simple:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services
    .AddMcpCore()
    .AddGitHubModule(builder.Configuration)
    .AddJiraModule(builder.Configuration)
    .AddKnowledgeModule(builder.Configuration);

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .AddAuthorizationFilters()
    .WithTools<RepositoryTools>()
    .WithTools<DocumentTools>()
    .WithTools<TicketTools>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapMcp("/mcp");

await app.RunAsync();
```

Exact APIs must follow the versions actually used by the project.

---

## 29. Testing Strategy

Required test layers:

```text
Unit Tests
Integration Tests
MCP Contract Tests
Architecture Tests
End-to-End Tests
```

### Unit Tests

Test:

- Core capabilities
- Policies
- Mapping logic
- Validation
- Error behavior

### Integration Tests

Test:

- Database integrations
- External API adapters
- Redis/cache
- Queue/task storage
- Authentication integration where practical

Prefer realistic infrastructure using containers when appropriate.

### MCP Contract Tests

Treat MCP tools as a public API.

Verify:

- Tool exists
- Tool name
- Description
- Input schema
- Required properties
- Output contract
- Authorization
- Validation
- Error behavior
- Cancellation
- Backward compatibility where required

### Architecture Tests

Automatically enforce rules such as:

```text
Core must not reference Protocol
Core must not reference Host
Core must not reference infrastructure implementations

Protocol must not directly access database implementations

Modules must not create uncontrolled cross-module dependencies

Consumer applications must not be project references of the MCP Server
```

Architecture rules must run in CI.

### End-to-End Tests

Verify real MCP flows through the server boundary.

---

## 30. CI/CD Expectations

Recommended pipeline:

```text
restore
   ↓
format check
   ↓
build Release
   ↓
unit tests
   ↓
architecture tests
   ↓
integration tests
   ↓
MCP contract tests
   ↓
security/dependency scan
   ↓
container build
   ↓
E2E tests
   ↓
publish/deploy
```

Use centralized package management.

Prefer deterministic builds.

---

## 31. Deployment

The MCP Server must be independently deployable.

Target containerized deployment.

The deployment should support:

- Multiple replicas
- Horizontal scaling
- Health probes
- Readiness probes
- Graceful shutdown
- External configuration
- Secret management
- OpenTelemetry export
- Stateless request handling

Do not rely on sticky sessions unless an explicitly documented MCP feature requires them.

---

## 32. Aspire

Aspire may be used for:

- Local orchestration
- Service Defaults
- OpenTelemetry defaults
- Health checks
- Service discovery
- Local Redis/database/container dependencies

Do not allow Aspire to leak into Core capability logic.

The platform must remain deployable without requiring application-domain coupling to Aspire.

---

## 33. Security Requirements

Apply least privilege.

At minimum consider:

- Authentication
- Scope/policy authorization
- Tool-level authorization
- Resource-level authorization where applicable
- Input validation
- Secret management
- Audit logging for sensitive operations
- Rate limiting
- Output/data filtering
- Injection protections
- SSRF protections for URL-capable tools
- Path traversal protections
- Safe command/process invocation if ever supported
- Dependency vulnerability scanning
- Secure defaults

Never expose arbitrary shell execution, arbitrary SQL, unrestricted filesystem access, or arbitrary outbound HTTP access without explicit controls and a strong business requirement.

---

## 34. Versioning and Compatibility

The MCP Server is a shared platform.

Changes may affect many consumers.

Design for compatibility.

For tool evolution:

- Prefer additive changes.
- Avoid silently changing semantics.
- Avoid renaming established tools without a migration path.
- Treat input/output schemas as contracts.
- Document breaking changes.
- Version capabilities when required.
- Maintain contract tests.

---

## 35. Module Independence

Modules should be independently understandable.

Example:

```text
GitHub Module
    Repository search
    File read
    PR information

Jira Module
    Ticket search
    Ticket read
    Ticket create

Knowledge Module
    Semantic search
    Document lookup
```

A GitHub module must not directly depend on Jira implementation details unless an explicit Core-level abstraction defines that relationship.

Avoid uncontrolled module-to-module coupling.

---

## 36. Expected Architecture Outcome

The final system should conceptually look like:

```text
                           Consumers

            ┌────────────────────────────────┐
            │ Apps / Agents / IDEs / Clients│
            └───────────────┬────────────────┘
                            │
                           MCP
                            │
                            ▼
                  ┌──────────────────┐
                  │    MCP Host      │
                  │ Auth / Transport │
                  └────────┬─────────┘
                           │
                           ▼
                  ┌──────────────────┐
                  │ MCP Protocol     │
                  │ Tools/Resources  │
                  │ Prompts/Filters  │
                  └────────┬─────────┘
                           │
                           ▼
                  ┌──────────────────┐
                  │      Core        │
                  │ Capabilities     │
                  │ Orchestration    │
                  │ Policies         │
                  └────────┬─────────┘
                           │
                  ┌────────┴─────────┐
                  │                  │
                  ▼                  ▼
          ┌──────────────┐    ┌──────────────┐
          │   Modules    │    │Infrastructure│
          │ GitHub/Jira  │    │ Cache/DB     │
          │ Search/etc.  │    │ Queue/Tasks  │
          └──────────────┘    └──────────────┘
```

---

## 37. Explicit Non-Goals

Do NOT:

- Embed the MCP Server into consumer applications.
- Create an `Application` project representing one consuming application.
- Share consumer business-layer assemblies with the MCP Server.
- Put database code directly in MCP tools.
- Put HTTP API calls directly in MCP tools.
- Put all tools into one giant class.
- Put all code into one project.
- Use static mutable state for request/session data.
- Depend on sticky sessions by default.
- Create microservices prematurely for every module.
- Introduce an interface for every class.
- Create a large generic `Common` or `Utils` library.
- Expose infrastructure exceptions externally.
- Blindly retry write operations.
- Ignore cancellation tokens.
- Allow one client to consume unlimited shared capacity.
- Couple Core to MCP SDK implementation details when avoidable.

---

## 38. Architectural Decision Summary

| Concern | Decision |
|---|---|
| Runtime | .NET 10 |
| Deployment | Standalone MCP service |
| Consumers | Multiple independent applications/agents |
| Application coupling | None |
| Hosting | ASP.NET Core |
| MCP transport | Streamable HTTP |
| Session model | Stateless by default |
| Architecture | Modular standalone platform |
| Stable center | `McpPlatform.Core` |
| MCP layer | Thin protocol adapter |
| Integrations | Modular adapters/providers |
| DI | Microsoft built-in DI |
| Configuration | Strongly typed Options |
| External HTTP | Typed `HttpClient` |
| Resilience | Standard .NET resilience policies |
| Observability | OpenTelemetry |
| Authentication | Standards-based |
| Authorization | Policy/scope/tool based |
| Long-running operations | Durable MCP Tasks |
| Horizontal scaling | Required |
| Architecture enforcement | Automated tests |
| Contracts | MCP contract tests |
| Package versions | Central package management |
| Local orchestration | Aspire where useful |

---

## 39. Instructions for the Implementing Agent

When implementing this architecture:

1. Preserve the standalone MCP Server boundary at all times.
2. Do not introduce dependencies on a consuming application.
3. Keep MCP protocol types at the boundary.
4. Keep Core independent of protocol and infrastructure implementations.
5. Add abstractions only at meaningful architectural seams.
6. Use explicit module registration.
7. Use stateless HTTP unless a documented requirement proves otherwise.
8. Propagate cancellation throughout every async operation.
9. Add production-grade observability from the beginning.
10. Add architecture tests before the project becomes large.
11. Treat MCP schemas and tool names as public API contracts.
12. Prefer simple, explicit code over unnecessary architectural patterns.
13. Every new module must define:
    - Capability ownership
    - Public MCP tools/resources/prompts
    - Core abstractions used
    - External dependencies
    - Authorization requirements
    - Failure modes
    - Observability
    - Tests
14. Every design decision that violates these requirements must be explicitly documented with its justification.

---

## 40. Definition of Done for Initial Architecture

The initial implementation is architecturally complete when:

- The .NET 10 solution builds successfully.
- The MCP Host runs independently.
- Streamable HTTP MCP is configured.
- Stateless operation is configured by default.
- At least one sample module demonstrates the required dependency direction.
- An MCP tool calls a Core abstraction rather than infrastructure directly.
- An integration implements that abstraction.
- Authentication/authorization wiring exists.
- Structured logging exists.
- OpenTelemetry tracing and metrics are wired.
- Health endpoints exist.
- Configuration uses validated Options.
- Cancellation flows end-to-end.
- Unit tests exist.
- Integration tests exist.
- MCP contract tests exist.
- Architecture dependency tests exist.
- End-to-end MCP testing exists.
- The server can run as multiple replicas without process-local critical state.
- No consumer application project is referenced by the MCP Server.
