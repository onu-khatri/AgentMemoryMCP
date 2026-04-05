# AgentSession.MCP

AgentSession.MCP is a .NET 10 MCP server that provides durable, local agent session management on the user machine. It enables an AI agent to create, resume, persist, and inspect long-running work sessions with memory, logs, and artifacts.

## Overview

This server implements the following MCP tools:

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

## Tool Usage (Recommended Order)

Typical usage across all tools:

1. `list_agent_sessions`: optional discovery of existing sessions.
2. `create_or_activate_session`: required first step to establish session context.
3. `read_agent_memory`: load persisted memory, recent logs, and artifact inventory.
4. `append_agent_memory`: persist durable facts, constraints, and decisions.
5. `log_agent_event`: record meaningful actions, transitions, and external interactions.
6. `create_agent_artifact`: persist reusable outputs (plans, analyses, summaries, extracted data).
7. `read_agent_artifact`: retrieve a specific saved artifact by name.
8. `list_agent_artifacts`: inspect available artifacts and metadata.
9. `save_final_plan`: persist a timestamped final implementation plan artifact.
10. `get_latest_final_plan`: fetch the most recently saved final plan for continuation.

## Architecture

The project is intentionally split into layers:

- `Tools/`: MCP transport-facing handlers.
- `Contracts/`: request/response/item DTOs used by MCP tools.
- `Services/`: application service and filesystem-backed storage implementation.
- `Interfaces/`: abstractions for service, store, YAML serializer, and file system.
- `Models/`: domain entities for session state, logs, and artifact metadata.
- `Helpers/`: ID sanitization, front matter parsing, and validation helpers.
- `Options/`: storage root configuration.

This supports SOLID principles, testability, and future migration to alternate storage backends.

## Contracts (Short)

MCP-facing DTOs are split by role under `Contracts/`:

- `Contracts/Requests/`: tool input payloads.
- `Contracts/Responses/`: tool output payloads.
- `Contracts/Items/`: shared nested/metadata payloads reused by requests and responses.

Contracts remain in the `AgentSession.MCP.Tools` namespace to keep tool signatures stable while allowing physical file/folder reorganization.

## Local Storage Layout

Default root path:

- `%USERPROFILE%/AgentMemory/sessions`

Per-session structure:

- `<root>/<session-id>/agent_session_state.yaml`
- `<root>/<session-id>/agent_memory.md`
- `<root>/<session-id>/agent_session_log.yaml`
- `<root>/<session-id>/artifacts/*.md`

Artifacts are stored as markdown files with YAML front matter.

## Concurrency and Safety

- Session and artifact names are sanitized to filesystem-safe segments.
- Path traversal attempts are blocked.
- Writes use atomic temp-file then replace/move behavior.
- Per-session synchronization (`SemaphoreSlim`) avoids concurrent write corruption.
- Recoverable parsing behavior skips malformed artifacts during list operations and logs warnings.

## Tool Contracts

### list_agent_sessions
Input:

```json
{}
```

Returns metadata list:

- `session_id`
- `created_at`
- `updated_at`
- `current_state_summary`
- `artifact_count`
- `last_log_timestamp`
- `description`

### create_or_activate_session
Input:

```json
{
  "sessionId": "weekly-planning",
  "initialState": "Working on sprint planning",
  "agentName": "copilot"
}
```

Output is a YAML string with:

- `session_id`
- `session_path`
- `created_or_activated`
- `current_state`
- `usage_instruction`
- `available_files`
- `artifact_folder_path`

### read_agent_memory
Input:

```json
{
  "sessionId": "weekly-planning",
  "latestLogEntries": 10
}
```

Returns full memory content, artifact inventory, latest logs, and session metadata.

### append_agent_memory
Input:

```json
{
  "sessionId": "weekly-planning",
  "content": "Decision: use incremental rollout.",
  "agentName": "copilot",
  "sectionTitle": "Release Strategy"
}
```

Returns success and updated memory size.

### log_agent_event
Input:

```json
{
  "sessionId": "weekly-planning",
  "message": "Created implementation plan",
  "agentName": "copilot",
  "level": "Info",
  "eventType": "planning",
  "correlationId": "req-123"
}
```

### create_agent_artifact
Input:

```json
{
  "sessionId": "weekly-planning",
  "artifactName": "implementation-plan",
  "description": "Sprint implementation plan",
  "intendedUse": "Execution checklist",
  "content": "# Plan\n- Step 1\n- Step 2",
  "contentType": "text/markdown",
  "tags": ["sprint", "plan"],
  "overwrite": false
}
```

### read_agent_artifact
Input:

```json
{
  "sessionId": "weekly-planning",
  "artifactName": "implementation-plan"
}
```

### list_agent_artifacts
Input:

```json
{
  "sessionId": "weekly-planning"
}
```

Returns artifact metadata:

- `name`
- `description`
- `intended_use`
- `content_type`
- `created_at`
- `updated_at`
- `tags`
- `file_name`

### save_final_plan
Input:

```json
{
  "sessionId": "weekly-planning",
  "planContent": "# Final Plan\n- implement feature\n- add tests",
  "planTitle": "Sprint Final Plan",
  "agentName": "copilot",
  "planVersion": "v1.2.0",
  "approvedDecisions": [
    "Use artifact-based handoff",
    "No breaking API changes"
  ],
  "acceptanceCriteria": [
    "All tests pass",
    "No regression in artifact tools"
  ],
  "outOfScope": ["Data migration", "Auth redesign"],
  "validationRequirements": ["dotnet build", "dotnet test"],
  "rollbackOrFallbackExpectations": ["Revert to previous artifact revision"],
  "constraints": {
    "project_knowledge": ["existing artifact storage patterns"],
    "architectural_constraints": ["preserve backward compatibility"]
  },
  "assumptions": ["no data migration required"],
  "impactAnalysis": {
    "affected_module_components": ["Tools", "Services", "Store"],
    "cross_cutting_concerns": ["validation", "serialization", "logging"],
    "potential_side_effects": ["larger front matter payload"]
  },
  "implementationStrategy": {
    "summary": "Additive extension using existing artifact flow",
    "approach": ["extend contracts", "map in service", "persist in metadata"]
  },
  "stepByStepPlan": {
    "notes": ["keep existing APIs stable"],
    "steps": [
      { "title": "Extend request model", "note": "add optional sections" },
      { "title": "Persist details", "note": "store in final_plan_details metadata" }
    ]
  },
  "technicalRisks": [
    { "risk": "metadata growth", "mitigation": "optional fields and concise entries" }
  ],
  "openQuestions": ["Should approval be mandatory for persistence?"],
  "requiredButSkippedDecisions": ["Plan retention policy"],
  "approval": {
    "is_approved": true,
    "approval_timestamp": "2026-04-04T12:00:00Z"
  }
}
```

Behavior:

- Creates a new timestamped artifact file for every save.
- Uses UTC timestamp naming with collision-safe suffix when needed.
- Returns `plan_version` when provided in request, enabling planner/executor handoff correlation.

### get_latest_final_plan
Input:

```json
{
  "sessionId": "weekly-planning"
}
```

Returns the most recently saved final plan by save timestamp metadata.
If no final plan exists, returns an explicit not-found response.
When available, response includes `plan_details` with plan version, approved decisions, acceptance criteria, out-of-scope boundaries, validation requirements, rollback/fallback expectations, constraints, assumptions, impact analysis, strategy, step-by-step notes/steps, risks, open questions, skipped decisions, and approval details.

## Run Locally

From repository root:

```powershell
dotnet run --project AgentSession.MCP/AgentSession.MCP.csproj
```

## Build, Test, Pack

```powershell
dotnet build AgentMemoryMCP.slnx
dotnet test AgentSession.MCP.Tests/AgentSession.MCP.Tests.csproj
dotnet pack AgentSession.MCP/AgentSession.MCP.csproj -c Release
```

## MCP Client Configuration Example

```json
{
  "mcpServers": {
    "agent-session": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "AgentSession.MCP/AgentSession.MCP.csproj"
      ]
    }
  }
}
```

## Guidance for Agents

- Always create or activate a session before long-running work.
- Read memory before resuming prior tasks.
- Append durable facts, decisions, and constraints.
- Log state-changing actions, external interactions, and errors.
- Save reusable outputs as artifacts.
- Do not persist transient chain-of-thought.

## Related Documentation

- Workspace overview: `../README.md`
- Prompt references: `../Prompts/MCP_For_Agent_Session.md`, `../Prompts/PlanSavingTools.md`
