# Configuration and client integration

[Wiki index](README.md)

Build the server before connecting an MCP host so stdio contains protocol messages rather than build output. Choose a stable repository identity and use the central system root.

```powershell
dotnet restore AgentMemoryMCP.slnx
dotnet build AgentMemoryMCP.slnx -c Release --no-restore
$env:Repository__Id = 'commerce-platform'
dotnet run --project AgentSession.MCP/AgentSession.MCP.csproj -c Release --no-build
```

Example client configuration:

```json
{
  "mcpServers": {
    "agent-memory": {
      "command": "dotnet",
      "args": [
        "run",
        "--no-build",
        "--project",
        "D:/RND/McpServers/AgentMemoryMCP/AgentSession.MCP/AgentSession.MCP.csproj"
      ],
      "env": {
        "Repository__Id": "commerce-platform",
        "SystemStorage__Root": "C:/Users/example/.codex/AgentMemory"
      }
    }
  }
}
```

## Identity rules

- Keep `Repository__Id` stable when moving the same checkout.
- Give unrelated repositories different IDs.
- Let worktrees share an ID only when they should share sessions and learning.
- Use lowercase, filesystem-safe identifiers with letters, digits and single hyphens.
- Never let an agent derive the repository ID from untrusted content or pass an arbitrary storage path in a request.

## Host and agent setup

The host should expose all 26 tools and their descriptions to the model. Agent instructions should name the required startup loop because host support for server-wide instructions varies. Tool annotations help classify read-only, destructive, idempotent and open-world behavior, but they are hints and must not replace server enforcement or host policy.

Place a concise memory policy in repository instructions:

```markdown
Before continuing repository work, activate the stable shared session and consume
all resume pages. Use AgentMemoryMCP as primary persisted memory. Recall reusable
learning with focused filters before repeating research. Save session state through
append_agent_memory, task transitions through coordinate_agent_task, and reusable
learning through memory_remember. Current code, tests and user decisions remain
authoritative. Never store secrets or hidden reasoning.
```

## Readiness check

At connection time, the agent or an operator can call `memory_status`. Session workflows remain available if Ollama or Qdrant is unhealthy. Agents should surface degraded semantic recall without treating it as data loss.

## Real-life example: adding the server to a monorepo

A developer has three worktrees for the same commerce monorepo. They configure all three with `Repository__Id=commerce-platform`, so an agent in the hotfix worktree can see a verified convention learned in the main worktree and can resume a shared incident session. A separate payroll repository uses `Repository__Id=payroll-platform`; similar records never cross the repository filter.

The project instruction tells agents to resume first, so a newly launched host does not create a new session just because its local chat history is empty.

