# Technology and design decisions

[Previous: Source catalogs](16-test-and-operations-source-catalog.md) | [Developer wiki](README.md) | [Next: Extension guide](18-extension-and-change-guide.md)

## .NET 10 and C#

The project uses .NET 10 for current runtime support, nullable reference analysis, modern JSON/schema APIs, `TimeProvider`, async I/O and self-contained multi-runtime publishing. C# records model immutable wire results while classes support JSON defaults and persisted aggregate mutation.

## Generic Host

The Generic Host centralizes configuration, DI, logging, graceful shutdown and `BackgroundService`. This fits a long-running stdio server better than manually constructing services and timers.

Tradeoff: singleton composition assumes one repository per process. Multi-repository hosting would require explicit tenancy throughout locks, paths, options and provider state.

## Official MCP C# SDK and stdio

`ModelContextProtocol` supplies hosting integration, typed schema generation, structured content and attribute-discovered tools. Stdio is local, simple and credential-free. The pinned stable SDK supports negotiated older and current protocol versions.

Tradeoff: stdout purity is strict, and remote access needs a different transport/security design.

## Filesystem canonical storage

Readable JSON files satisfy local ownership, backup, inspection and provider independence. Atomic replacement plus transaction intents provides production safety without a database service.

Tradeoff: global repository locking limits write concurrency and large catalogs need careful bounds. A future database backend must preserve the same revision, idempotency, recovery and audit semantics.

## Strict System.Text.Json

One serializer governs wire and persisted data. Unknown properties and numeric enums are rejected, which catches obsolete clients and typos. Generated JSON Schema keeps MCP discovery aligned with DTOs.

Tradeoff: contract evolution must be deliberate; additive fields require coordinated schema/version decisions.

## Optimistic revisions plus idempotency

Revisions prevent stale semantic overwrites. Operation receipts make network retries safe. Using both solves different problems: revision identifies current state, while operation ID identifies caller intent.

## Roll-forward recovery

Once an intent is durable, the system finishes rather than rolls back. File replacement is naturally atomic per file; replaying a deterministic mutation list is simpler and safer than trying to reconstruct previous versions after a crash.

## Ollama

Ollama provides local embeddings without cloud credentials. The adapter discovers the model digest, forbids truncation and validates every returned vector.

Tradeoff: local model availability and performance vary; lexical degraded mode is required.

## Qdrant

Qdrant provides cosine vector search, payload filtering, deterministic point IDs, typed payload indexes and atomic aliases. Physical collections are fingerprinted; a stable alias enables verified model migration.

Tradeoff: Qdrant is an extra local service. Therefore it is explicitly derived and rebuildable, never the only copy of learning.

## Partial CanonicalMemoryService

The aggregate service is split by feature across partial files so shared invariants and commit helpers remain internal without creating a large public interface graph.

Tradeoff: maintainers must understand that methods across files share injected dependencies and private helpers. The source catalog groups them as one service.

## Dedicated session coordination

Session tasks use leases and fencing rather than generic artifacts. This prevents an agent from claiming ownership by writing arbitrary JSON. Artifacts remain flexible for plans/findings, while task transitions remain constrained.

## External primary references

- [.NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
- [Official MCP SDK list](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/docs/2026-07-28/sdk.mdx)
- [MCP C# SDK getting started](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md)
- [Qdrant points](https://qdrant.tech/documentation/concepts/points/)
- [Qdrant collections and aliases](https://qdrant.tech/documentation/manage-data/collections/)
- [Qdrant payload](https://qdrant.tech/documentation/concepts/payload/)
- [Ollama embedding API](https://docs.ollama.com/api/embed)
- [Ollama embedding guidance](https://docs.ollama.com/capabilities/embeddings)

