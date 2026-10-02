# Build, run and demonstration script

[Previous: Troubleshooting](19-troubleshooting.md) | [Developer wiki](README.md) | [Next: Mind map](21-project-mind-map.md)

## Prerequisites

- .NET SDK 10
- PowerShell 7 for repository scripts
- Ollama with `embeddinggemma` for semantic demonstrations
- Docker/Qdrant for semantic demonstrations
- An MCP host that can launch a stdio server

Session and canonical lexical demonstrations do not require Ollama or Qdrant.

## Build and standard tests

```powershell
dotnet restore AgentMemoryMCP.slnx
dotnet build AgentMemoryMCP.slnx -c Release --no-restore
dotnet test AgentSession.MCP.Tests/AgentSession.MCP.Tests.csproj -c Release --no-restore
```

## Start semantic dependencies

```powershell
$root = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex/AgentMemory'
ollama pull embeddinggemma
./scripts/Start-AgentMemoryQdrant.ps1 -SystemRoot $root
./scripts/Test-AgentMemoryDependencies.ps1 -SystemRoot $root -EmbeddingModel embeddinggemma
```

## Configure a host

```json
{
  "mcpServers": {
    "agent-memory-demo": {
      "command": "dotnet",
      "args": [
        "run",
        "--no-build",
        "--project",
        "D:/RND/McpServers/AgentMemoryMCP/AgentSession.MCP/AgentSession.MCP.csproj",
        "-c",
        "Release"
      ],
      "env": {
        "Repository__Id": "agent-memory-demo",
        "SystemStorage__Root": "C:/Users/example/.codex/AgentMemory"
      }
    }
  }
}
```

## Ten-minute demo

### Discover the surface

Ask the host to list tools. Show six session tools and twenty memory tools with structured schemas and annotations. Explain that tool methods contain no business logic.

### Create and persist a session

Call `create_or_activate_session` for `demo-session`, then use `append_agent_memory` to create `context` and `plan-demo-v1` in one batch. Show returned sequence and artifact revisions.

Call `resume_agent_session` and follow all pages. Point out snapshot ID, sequence, entries and `hasMore`. Call checkpoint only after the last page.

### Demonstrate coordination

Create `demo-tests` with work key and acceptance expectations. Claim it as `test-agent`, capture the claim token, save a task-linked output artifact and complete with evidence. Resume as another actor and show that it can reuse the output.

### Demonstrate reusable learning

Call `memory_remember` with short tier:

```json
{
  "operationId": "remember-demo-convention",
  "tier": "short",
  "title": "Demo repository uses UTC evidence timestamps",
  "content": "All persisted evidence timestamps in the demo repository use UTC offsets.",
  "sessionId": "demo-session",
  "taskId": "demo-tests",
  "agentId": "test-agent",
  "category": "repository-convention",
  "decisionArea": "persistence",
  "tags": ["utc", "evidence"],
  "sourceType": "demo-test",
  "sourceReference": "artifact:output-demo-tests"
}
```

Recall it with a focused query and show `advisory: true`, match type and score behavior.

### Demonstrate restart continuity

Stop the MCP process without deleting storage. Restart the host with the same repository ID. Activate and resume `demo-session`. Show the plan, completed task, output, checkpoint and reusable memory remain available.

### Demonstrate semantic behavior

Create/promote an eligible long candidate, process or migrate the vector index, then query with a paraphrase. Show repository filtering and canonical hit verification. Stop Qdrant and repeat recall to show labeled lexical degradation rather than data loss.

### Demonstrate status and files

Call `memory_status`. Inspect, without editing, the repository's central session and `AiLearning` directories. Explain canonical records, catalog shards, operations, events, outcomes, pending vectors and active collection metadata.

## Publish smoke

```powershell
dotnet publish AgentSession.MCP/AgentSession.MCP.csproj -c Release -r win-x64 --self-contained true
./scripts/Test-PublishedMcp.ps1 -ExecutablePath <published-executable> -RepositoryId agent-memory-demo
```

The smoke should discover 26 unique tools and show clean protocol stdout.

## Optional observability demonstration

Start and check the disposable local collector, enable loopback `http/protobuf` export, run a successful and a validation-failure call, then stop the collector. Show receiver accepted counts, bounded JSON stderr, trace/log correlation, and that the tmpfs sink disappears on stop. Follow the canonical [observability operations guide](../../docs/observability/README.md); do not use production credentials or persistent storage for the demo.

## Demo explanation checklist

Be ready to explain why activation is separate from resume, why checkpoint does not save work, why operation ID and revision are both needed, why Qdrant is derived, and what survives each simulated failure.
