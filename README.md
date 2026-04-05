# AgentMemoryMCP

AgentMemoryMCP is a .NET 10 workspace for MCP servers that provide durable local memory and session continuity for AI agents.

## Included MCP Tools

The `AgentSession.MCP` server currently provides these tools:

- `list_agent_sessions`
- `create_or_activate_session`
- `read_agent_memory`
- `append_agent_memory`
- `log_agent_event`
- `create_agent_artifact`
- `read_agent_artifact`
- `list_agent_artifacts`
- `save_final_plan`
- `get_latest_final_plan`

## Tool Usage Quick Flow

Use the tools in this order for most agent workflows:

1. Optional discovery: call `list_agent_sessions` to inspect existing sessions.
2. Start or resume: call `create_or_activate_session` before any multi-step work.
3. Rehydrate context: call `read_agent_memory` when resuming prior work.
4. Persist evolving context: call `append_agent_memory` for durable facts and `log_agent_event` for traceable actions.
5. Work with reusable outputs: call `create_agent_artifact`, `read_agent_artifact`, and `list_agent_artifacts`.
6. Finalize plans: call `save_final_plan` to persist the latest final plan version.
7. Resume planning context later: call `get_latest_final_plan` to fetch the newest saved plan.

## Repository Structure

- `AgentSession.MCP/`: main MCP server project with tools, services, models, and storage logic.
- `AgentSession.MCP.Tests/`: test project for service, store, and tool behavior.
- `Prompts/`: prompt and workflow notes used during development.
- `AgentMemoryMCP.slnx`: solution entry point.

## Contracts (Short)

Tool request/response contracts are grouped under:

- `AgentSession.MCP/Contracts/Requests/`
- `AgentSession.MCP/Contracts/Responses/`
- `AgentSession.MCP/Contracts/Items/`

These DTOs define MCP-facing payloads used by the tool handlers.

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

## Storage Layout

Default local storage root:

- `%USERPROFILE%/AgentMemory/sessions`

Per-session files:

- `<root>/<session-id>/agent_session_state.yaml`
- `<root>/<session-id>/agent_memory.md`
- `<root>/<session-id>/agent_session_log.yaml`
- `<root>/<session-id>/artifacts/*.md`

Artifacts are markdown files with YAML front matter metadata.

## Documentation

For tool contracts, storage layout, and usage examples, see:

- `AgentSession.MCP/README.md`
- `Prompts/MCP_For_Agent_Session.md`
- `Prompts/PlanSavingTools.md`
