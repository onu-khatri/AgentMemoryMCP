# AgentSession.MCP

The .NET 10 stdio server hosts shared-session coordination and tiered repository memory. Build before connecting an MCP client so stdout contains protocol messages only; diagnostics are written to stderr.

```powershell
dotnet build src/AgentSession.MCP.StdioHost/AgentSession.MCP.StdioHost.csproj -c Release
$env:Repository__Id = 'agent-memory-mcp'
dotnet run --project src/AgentSession.MCP.StdioHost/AgentSession.MCP.StdioHost.csproj -c Release --no-build
```

Runtime storage defaults to `%USERPROFILE%/.codex/AgentMemory`; set `SystemStorage__Root` to an absolute local root for isolation. The process binds one stable `Repository__Id`, and request payloads cannot select another repository.
