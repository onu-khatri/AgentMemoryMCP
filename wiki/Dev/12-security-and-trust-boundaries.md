# Security and trust boundaries

[Previous: Domain models](11-domain-models-and-contracts.md) | [Developer wiki](README.md) | [Next: Hosted maintenance](13-hosted-maintenance-and-status.md)

## Trust model

This is a local server for a trusted local user. It still treats request content, identifiers, recalled text and external dependencies defensively. Actor IDs provide provenance, not authentication. Tool annotations are hints, not enforcement.

## Path boundary

`MemoryConfigurationExtensions` rejects network/device/root paths. `ManagedStoragePathResolver` rejects traversal, rooted components, separators, ADS-like colons, invalid names, unsafe identifiers and links/reparse points. Repository identity is process-bound and compared on each managed path.

## Content boundary

`MemoryContentPolicy` recursively serializes and inspects request/mutation content. It rejects:

- common secret-key property names with values;
- private-key blocks;
- authorization headers and bearer/basic tokens;
- common cloud/GitHub/OpenAI key patterns;
- password/API-token assignments;
- sensitive patterns hidden in nested JSON strings;
- supported URL/base64 encoded variants.

Regex scanning has a timeout and depth is bounded. Detection is conservative and cannot prove content is secret-free.

## Authorization grants

`OperatorGrantVerifier` reads a small external grant file below `.authorizations/<repository-id>`. A valid grant matches schema version, repository, memory ID, current revision, action, named operator and unexpired UTC time.

Actions are protected deletion, protected archive and high-risk learning validation. The managed transaction API reserves grant paths, so MCP tools cannot manufacture their own authority.

## Semantic dependency boundary

Ollama and Qdrant must use validated loopback endpoints. The Ollama HTTP handler disables proxying and redirects. Embedding inputs are content-policy checked again. Qdrant results return identifiers and metadata only, then canonical files are revalidated.

## Evidence boundary

Evidence and output references are opaque strings. The server never dereferences them or independently declares them verified. Task completion rejects non-`unverified` status without evidence, but evidence correctness remains the caller's responsibility.

## Failure disclosure

Stable errors report codes and safe explanations without including stored content. Background logs deliberately exclude memory content. Deletion events contain identifiers/metadata rather than deleted text.

## Threat examples and controls

| Threat | Control |
| --- | --- |
| `../../outside` session ID | Identifier and path-component rejection |
| Root replaced by junction | Ancestor reparse-point check at operation time |
| Two processes overwrite one record | OS repository lock + expected hashes |
| Retry creates duplicates | Operation receipt and request hash |
| Stale task owner completes work | Claim token, expiry and fencing generation |
| Qdrant hit points to old revision | Canonical revision/content-hash verification |
| Caller says “approved” | External operator grant required |
| Secret hidden in JSON/base64 | Recursive/encoded content inspection |

## Security test locations

- [ManagedStoragePathTests.cs](../../AgentSession.MCP.Tests/ManagedStoragePathTests.cs)
- [MemoryContentPolicyTests.cs](../../AgentSession.MCP.Tests/MemoryContentPolicyTests.cs)
- [RepositoryMutationLockTests.cs](../../AgentSession.MCP.Tests/RepositoryMutationLockTests.cs)
- [CanonicalMemoryTests.cs](../../AgentSession.MCP.Tests/CanonicalMemoryTests.cs)

