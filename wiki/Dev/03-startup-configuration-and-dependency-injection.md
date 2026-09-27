# Startup, configuration and dependency injection

[Previous: Architecture](02-solution-architecture.md) | [Developer wiki](README.md) | [Next: Storage and transactions](04-storage-concurrency-and-transactions.md)

## Composition root

[Program.cs](../../AgentSession.MCP/Program.cs) uses `Host.CreateApplicationBuilder`. It clears default log providers, sends console logs to stderr, registers storage/configuration services, adds `MemoryMaintenanceService`, adds the MCP server, selects stdio transport and discovers both tool classes with the shared `MemoryJson.Options` serializer.

Stdout must remain JSON-RPC only. Any `Console.WriteLine`, build output or default console logger on stdout can corrupt the protocol stream.

```mermaid
flowchart LR
    A[Process starts] --> B[Create Generic Host builder]
    B --> C[Configure stderr logging]
    C --> D[Bind and validate options]
    D --> E[Register singleton services and HttpClient]
    E --> F[Register hosted maintenance]
    F --> G[Register MCP stdio and tools]
    G --> H[Build and run host]
```

## Configuration sections

| Section | Class | Important defaults |
| --- | --- | --- |
| `SystemStorage` | `SystemStorageOptions` | `%USERPROFILE%/.codex/AgentMemory` |
| `Repository` | `RepositoryOptions` | ID required; no path-derived fallback |
| `SessionCoordination` | `SessionCoordinationOptions` | 15-minute claim, 60-minute snapshot |
| `Memory:Temp` | `TempMemoryOptions` | 24-hour maximum, 60-minute cleanup |
| `Memory:Short` | `ShortMemoryOptions` | review 3 days, compact 7, archive 14, gzip |
| `Memory:Long` | `LongMemoryOptions` | two independent confirmations, ten default results |
| `Memory:Limits` | `MemoryLimits` | 64 KiB content, 1 MiB record, depth 32, batch 100 |
| `Embedding` | `EmbeddingOptions` | Ollama, 30 seconds, batch 32, `embeddinggemma` |
| `Qdrant` | `QdrantOptions` | localhost gRPC 6334, REST 6333 |

Environment variables use double underscores, for example `Repository__Id` and `SystemStorage__Root`.

## Startup validation

`MemoryConfigurationExtensions.AddMemoryStorageConfiguration` calls `ValidateOnStart` for every option group. Invalid roots, missing repository identity, non-loopback semantic endpoints, unsupported providers, excessive temp lifetime and inconsistent bounds stop startup before tools are exposed.

This fail-fast design prevents a request from discovering a dangerous configuration only after partial writes.

## Registered service graph

Singletons include path resolution, locking, content policy, transaction store, validators, catalog, canonical memory, grants, session services, semantic services, status and maintenance state. Singleton lifetime is appropriate because the process is bound to one repository and services contain no per-request user state.

`AddHttpClient<OllamaEmbeddingClient>` supplies pooled HTTP transport while the client method owns its explicit timeout. Redirects and proxies are disabled to keep the configured loopback trust boundary.

`QdrantClient` is constructed from the validated loopback host and gRPC port.

## Adding configuration safely

When adding an option:

1. Add it to an options class with a safe default where appropriate.
2. Bind it in `MemoryConfigurationExtensions`.
3. Add finite-range and relationship validation.
4. Add startup tests in `MemoryConfigurationTests`.
5. Document the environment variable and operational effect.
6. Avoid request-controlled roots, repository IDs or provider endpoints.

## Why .NET Generic Host

The Generic Host supplies configuration, dependency injection, logging, application lifetime and `BackgroundService` support in one composition model. It also allows the MCP SDK's hosting extensions and the maintenance worker to share graceful shutdown cancellation.

