
Please implement a new plan-memory capability in this project.

Requirement:
1. Add a tool to save an agent’s final implementation plan.
2. Each saved plan must be written as a separate file.
3. The saved filename must include a date-time suffix so files are unique and sortable.
4. Add a tool to fetch the latest saved final plan for a session.
5. “Latest” must be determined by actual save timestamp (not just lexical name order, unless naming format guarantees chronological sort).
6. Return a clear not-found response when no plan exists for that session.

Implementation expectations:
1. Reuse existing session/artifact storage patterns and conventions in this codebase instead of creating a parallel storage system.
2. Keep backward compatibility with existing tools and contracts.
3. Validate and sanitize session and plan identifiers using existing helper patterns.
4. Ensure safe behavior under concurrent saves.
5. Use UTC timestamps in filename and metadata.
6. Keep naming consistent with existing MCP tool naming style.

Suggested tool APIs:
1. save_final_plan
- Inputs: sessionId, planContent, optional planTitle, optional agentName
- Behavior: creates a new file every time (no overwrite), filename includes timestamp
- Output: success, sessionId, stored file/artifact name, savedAt

2. get_latest_final_plan
- Inputs: sessionId
- Behavior: returns most recently saved final plan for that session
- Output: success, sessionId, latest plan metadata, plan content
- If none: explicit not-found result with helpful message

Filename convention:
Use a deterministic and sortable UTC timestamp format, for example:
final-plan-YYYYMMDD-HHMMSS-fff.md
If a collision still occurs, append a short unique suffix.

Testing requirements:
1. Save two or more plans in same session and verify separate files are created.
2. Verify latest retrieval returns the newest plan.
3. Verify no-plan case returns expected not-found response.
4. Verify invalid sessionId input is rejected consistently with current validation behavior.
5. Verify concurrent saves do not corrupt data and all plans remain retrievable.
6. Add/extend tests at tool layer and service/store layer following existing test style.

Definition of done:
1. New save and fetch-latest tools are exposed and wired through tools, contracts, service, and store layers.
2. All tests pass, including new test coverage for this feature.
3. No regression to existing memory/artifact functionality.
4. Implementation summary includes impacted files and rationale for design choices.