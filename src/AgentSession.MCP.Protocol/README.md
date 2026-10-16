# AgentSession.MCP.Protocol

This project owns the inbound MCP boundary. It contains the explicit Session and AgentMemory tool adapters, MCP attributes and schemas, protocol bounds, stable error mapping, capability visibility, and the SDK request filters.

Protocol references only Core, Session.Contracts, AgentMemory.Contracts, and `ModelContextProtocol`. Persistence, provider clients, module implementations, authentication composition, and telemetry hosting remain outside this project.

`ProtocolRegistration.AddAgentSessionProtocol` is the single public registration path. It registers the two grouped adapter types explicitly, enables only tool capabilities, rejects a composed manifest that is not the authoritative 26-tool inventory, and requires zero resources and prompts. SDK `AddCallToolFilter` is retained only to reject unknown or unregistered calls; explicit adapters authorize inside the shared adapter pipeline before invoking a module contract.

Boundary validation applies safe request serialization, JSON depth, record, collection, page, result, timeout, and cancellation bounds. Module validators remain authoritative for business rules. Protocol errors expose stable categories and never raw paths, providers, stacks, or exception details.

The legacy `AgentSession.MCP/Tools` definitions are replaced by this project. HTTP hosting, authentication composition, deployment, and the StdioHost migration are intentionally deferred to task 8.
