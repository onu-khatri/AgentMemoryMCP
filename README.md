# AgentMemoryMCP

AgentMemoryMCP is a .NET 10 workspace for MCP servers that provide durable local memory and session continuity for AI agents.

## Repository Structure

- `AgentSession.MCP/`: main MCP server project with tools, services, models, and storage logic.
- `AgentSession.MCP.Tests/`: test project for service, store, and tool behavior.
- `Prompts/`: prompt and workflow notes used during development.
- `AgentMemoryMCP.slnx`: solution entry point.

## Quick Start

Run the MCP server:

```powershell
dotnet run --project AgentSession.MCP/AgentSession.MCP.csproj
```

Build the solution:

```powershell
dotnet build AgentMemoryMCP.slnx
```

Run tests:

```powershell
dotnet test AgentSession.MCP.Tests/AgentSession.MCP.Tests.csproj
```

Pack the MCP server:

```powershell
dotnet pack AgentSession.MCP/AgentSession.MCP.csproj -c Release
```

## Documentation

For tool contracts, storage layout, and usage examples, see:

- `AgentSession.MCP/README.md`
