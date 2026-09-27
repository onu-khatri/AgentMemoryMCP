# AgentSession.MCP

The .NET 10 stdio server hosts shared-session coordination and tiered repository memory in one process. Build before connecting an MCP client so stdout contains protocol messages only; diagnostics are written to stderr.

```powershell
dotnet restore ../AgentMemoryMCP.slnx
dotnet build AgentSession.MCP.csproj -c Release --no-restore
$env:Repository__Id = 'agent-memory-mcp'
dotnet run --project AgentSession.MCP.csproj -c Release --no-build
```

Runtime storage defaults to `%USERPROFILE%/.codex/AgentMemory`. Set `SystemStorage__Root` to an absolute local root for isolation. The process binds one stable `Repository__Id`; request payloads cannot select another repository.

Session discovery, structured artifacts, fenced task ownership, resume snapshots and checkpoints remain available while Ollama or Qdrant is offline. The 20 memory tools manage canonical temp, short and long-term learning; semantic recall uses local Ollama `embeddinggemma` and repository-scoped Qdrant collections, with labeled lexical fallback when dependencies are unavailable.

Use the server as the agent's primary persisted memory source. At startup, discover an existing session if needed, activate it, consume every `resume_agent_session` page, then run focused `memory_recall` queries before reconstructing known work. Save session progress and handoffs with `append_agent_memory`; save reusable cross-session learning with `memory_remember`; change task state only with `coordinate_agent_task`; checkpoint only after consuming the full snapshot. This workflow reduces prompt growth and repeated work while keeping current repository evidence and user instructions authoritative.

Read the [developer onboarding wiki](../wiki/Dev/README.md), [agent and skill wiki](../wiki/agent-ai-guidence/README.md), [MCP contract](../AiLearning/MCP-CONTRACT.md), [parent/sub-agent workflow](../AiLearning/PARENT-SUBAGENT-EXAMPLE.md), [operations guide](../AiLearning/OPERATIONS.md), and [runtime layout](../AiLearning/README.md).

Memory is advisory and never replaces current repository evidence, tests, instructions, approvals or user decisions. Persist conclusions and evidence rather than secrets or private chain-of-thought. Startup leaves historical storage untouched.
