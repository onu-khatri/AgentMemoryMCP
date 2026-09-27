# External standards and references

[Wiki index](README.md)

Repository behavior is defined first by the implementation and its contract:

- [MCP contract](../../AiLearning/MCP-CONTRACT.md)
- [Parent and sub-agent example](../../AiLearning/PARENT-SUBAGENT-EXAMPLE.md)
- [Operations guide](../../AiLearning/OPERATIONS.md)
- [Legacy reuse decisions](../../AiLearning/LEGACY-REUSE.md)

The following primary MCP sources explain general protocol behavior that informs custom clients and agents:

- [MCP 2026-07-28 specification release](https://blog.modelcontextprotocol.io/posts/2026-07-28/) describes the current protocol generation, JSON Schema 2020-12 tool contracts, stateless core and authorization updates.
- [Tool annotations as risk vocabulary](https://blog.modelcontextprotocol.io/posts/2026-03-16-tool-annotations/) explains that read-only, destructive, idempotent and open-world annotations are useful hints rather than enforcement.
- [Server instructions guidance](https://blog.modelcontextprotocol.io/posts/2025-11-03-using-server-instructions/) explains why clear server usage instructions improve model behavior, subject to host support.
- [MCP TypeScript SDK overview](https://ts.sdk.modelcontextprotocol.io/v2/) provides a current host/server mental model and identifies its v2 line with the 2026-07-28 specification.
- [MCP prompts documentation](https://ts.sdk.modelcontextprotocol.io/v2/servers/prompts) distinguishes user-selected prompts from model-selected tools.

## How to apply external guidance here

- Use tool descriptions and schemas to help the model select correct calls.
- Treat annotations as input to host policy, never as proof that an operation is safe.
- Put the mandatory resume/write/checkpoint workflow in custom agent and project instructions because server-instruction support varies by host.
- Keep authorization, repository isolation, revision checks and grants enforced by the server rather than relying on prompt compliance.
- Test behavior against every MCP protocol version supported by this project.

## Real-life example: host ignores server instructions

A developer's MCP host exposes tools but does not inject server-wide instructions into the model context. The project still works because the repository's custom-agent instructions contain the startup loop, and every tool description states when to use it and what to avoid. The host uses annotations to improve confirmation UX but relies on its sandbox and the server's revision, path and grant checks for enforcement.

## Version caution

External MCP guidance evolves. Before changing transport, authorization, schema or annotation behavior, verify the current official specification and the pinned .NET SDK used by this repository. Do not infer that an external SDK example changes this server's implemented contract.

