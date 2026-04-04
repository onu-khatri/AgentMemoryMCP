Role:
You are a senior .NET platform engineer and MCP server architect.

Goal:
Design and implement a production-ready MCP server in .NET that provides local Agent Session Management on the user’s machine. The server must allow an AI agent to create, resume, manage, and persist long-running work sessions with memory, logs, state, and artifacts.

Primary objective:
Enable an agent to preserve continuity across conversations and tasks by storing important session context, reusable knowledge, operational logs, and generated artifacts locally on the user system.

Core requirements:
1. Implement an MCP server with tools for:
   - list_agent_sessions
   - create_or_activate_session
   - read_agent_memory
   - append_agent_memory
   - log_agent_event
   - create_agent_artifact
   - read_agent_artifact
   - list_agent_artifacts

2. Session behavior:
   - Sessions are stored locally on the user machine using files and folders.
   - Each session must have a dedicated folder named by sessionId.
   - sessionId must be human-readable, filesystem-safe, and deterministic when provided by the user.
   - If sessionId is not provided, generate a human-readable unique sessionId.
   - If a provided sessionId already exists, activate that session instead of creating a duplicate.
   - If a provided sessionId does not exist, create it automatically.
   - A session acts as the agent’s long-term workspace and continuity boundary.

3. Persistence expectations:
   - All important data must persist locally across process restarts.
   - Store:
     - session metadata/state in YAML
     - memory in Markdown
     - log entries in YAML
     - artifacts as individual Markdown or YAML files with front matter
   - All file writes must be durable and safe against corruption where practical.
   - Use atomic write patterns where possible.

4. Real-world expectations:
   - Production-ready structure
   - Clean architecture and maintainable design
   - Strong validation and error handling
   - Concurrency-safe access for parallel tool calls
   - Filesystem-safe naming rules
   - Structured logging
   - Unit-testable design
   - Clear extension points for future storage backends
   - Helpful tool descriptions for AI agent usage

Technical requirements:
1. Tech stack:
   - .NET 8+
   - C#
   - MCP server implementation in .NET
   - YAML serialization
   - JSON-compatible DTOs where applicable

2. Local storage structure:
   Use a root path like:
   %USERPROFILE%/.moltiAgent/sessions

   Each session folder should contain at minimum:
   - agent_session_state.yaml
   - agent_memory.md
   - agent_session_log.yaml
   - artifacts/
   - optional indexes/metadata files if needed

3. Suggested domain models:
   Use and improve the following models where useful:

   internal class AgentSessionLogEntry
   {
       public DateTime Timestamp { get; set; }
       public string Message { get; set; } = string.Empty;
       public string AgentName { get; set; } = string.Empty;
       public string Level { get; set; } = "Info";
       public string? EventType { get; set; }
       public string? CorrelationId { get; set; }
   }

   internal class AgentSession
   {
       public string SessionId { get; set; } = string.Empty;
       public DateTime CreatedAt { get; set; }
       public DateTime UpdatedAt { get; set; }
       public string CurrentState { get; set; } = string.Empty;
   }

   public class AgentArtifact
   {
       public string Name { get; set; } = string.Empty;
       public string Description { get; set; } = string.Empty;
       public string IntendedUse { get; set; } = string.Empty;
       public string ContentType { get; set; } = "text/markdown";
       public List<string> Tags { get; set; } = new List<string>();
       public DateTime CreatedAt { get; set; }
       public DateTime UpdatedAt { get; set; }
       public string FileName { get; set; } = string.Empty;
   }

4. Improve the model and design where required:
   - Add metadata like created_at, updated_at, agent_name, tags, version, content_type
   - Add session summary/index if useful
   - Add artifact metadata model
   - Add validation helpers
   - Add file path abstraction for testability
   - Add storage service abstraction

Tool contract requirements:
Implement each MCP tool with clear input/output contracts.

1. list_agent_sessions
   Purpose:
   - Return all existing sessions available on disk.

   Output:
   - List of sessions with metadata:
     - session_id
     - created_at
     - updated_at
     - current_state summary
     - artifact_count
     - last_log_timestamp
     - short description if available

2. create_or_activate_session
   Input:
   - sessionId: optional
   - initialState: optional
   - agentName: optional

   Behavior:
   - If sessionId is null/empty, create a new session with generated human-readable id.
   - If sessionId exists, activate existing session.
   - If sessionId does not exist, create it.
   - Ensure all required files/folders exist.
   - Return active session metadata and usage instructions.

   Output:
   - Serialize response to YAML string
   - Include:
     - session_id
     - session_path
     - created_or_activated flag
     - current_state
     - usage_instruction
     - available_files
     - artifact_folder_path

3. read_agent_memory
   Input:
   - sessionId

   Behavior:
   - Read full persisted session memory
   - Read list of artifacts and metadata
   - Optionally include latest logs summary
   - Should be the first tool used when resuming meaningful prior work

   Output:
   - Full memory content
   - Artifact inventory
   - Relevant session metadata

4. append_agent_memory
   Input:
   - sessionId
   - content
   - agentName: optional
   - sectionTitle: optional

   Behavior:
   - Append durable knowledge only
   - Avoid overwriting existing memory unless explicitly designed
   - Timestamp entries
   - Preserve readability of memory file

   Output:
   - Success confirmation
   - Updated memory size/metadata

5. log_agent_event
   Input:
   - sessionId
   - message
   - agentName
   - level (Info, Warning, Error, Debug)
   - eventType optional
   - correlationId optional

   Behavior:
   - Append structured log entry to persistent session log
   - Use UTC timestamps
   - Keep logs machine-readable and human-readable

   Output:
   - Success confirmation

6. create_agent_artifact
   Input:
   - sessionId
   - artifactName
   - description
   - intendedUse
   - content
   - contentType optional
   - tags optional

   Behavior:
   - Create or update an artifact file in the session artifacts folder
   - Store front matter metadata plus body
   - Artifact name must be filesystem-safe
   - Prevent accidental destructive overwrite unless explicitly intended

   Output:
   - Success confirmation
   - Stored artifact metadata
   - File path

7. read_agent_artifact
   Input:
   - sessionId
   - artifactName

   Behavior:
   - Read one artifact by exact name
   - Return clear not-found error if missing

   Output:
   - Artifact metadata
   - Full content

8. list_agent_artifacts
   Input:
   - sessionId

   Output:
   - List of artifact metadata:
     - name
     - description
     - intended_use
     - content_type
     - created_at
     - updated_at
     - tags
     - file_name

Production design constraints:
1. Architecture:
   - Separate MCP tool layer, application/services layer, and storage/infrastructure layer
   - Keep business logic out of transport/tool handlers
   - Prefer interfaces for storage and serialization services

2. Filesystem and safety:
   - Sanitize sessionId and artifactName
   - Prevent path traversal
   - Validate all inputs
   - Handle missing files gracefully
   - Create directories lazily but safely
   - Use file locks or per-session synchronization to prevent concurrent write corruption

3. Reliability:
   - Writes should be resilient
   - Avoid partial file corruption
   - Use temp files + replace where appropriate
   - Handle malformed YAML/Markdown with recoverable errors
   - Return explicit tool errors, not silent failures

4. Observability:
   - Add internal application logs
   - Include correlation identifiers when useful
   - Make debugging easy

5. Extensibility:
   - Design so local file storage can later be replaced with SQLite, LiteDB, or cloud storage
   - Keep serialization and path layout configurable
   - Allow future support for artifact search or memory compaction

6. Testing:
   - Add unit tests for:
     - session creation
     - session activation
     - memory append/read
     - artifact creation/read/list
     - invalid names
     - concurrent writes
     - missing session handling
   - Prefer temp-directory based tests

7. Documentation:
   - Add README with:
     - overview
     - architecture
     - storage layout
     - tool descriptions
     - example requests/responses
     - local run/debug instructions
   - Add sample MCP tool payloads

Implementation deliverables:
1. Full project structure
2. Complete C# implementation
3. Tool registration
4. DTOs and domain models
5. Storage services
6. Serialization helpers
7. Validation utilities
8. Tests
9. README
10. Example outputs

Important behavioral guidance for the agent using this server:
- Always initialize or activate a session before long-running work.
- Read agent memory before continuing a previous task.
- Append durable knowledge, constraints, decisions, and reusable findings regularly.
- Log actions that change state, external interactions, errors, or important execution milestones.
- Create artifacts for summaries, plans, mappings, extracted structured knowledge, or reusable outputs.
- Do not store transient chain-of-thought or disposable intermediate reasoning as artifacts.

Expected output from you:
Provide a complete implementation proposal and code for this MCP server, including:
- architecture explanation
- project structure
- full implementation
- test strategy
- example tool inputs/outputs
- design decisions and tradeoffs

Code quality expectations:
- Production-grade naming
- SOLID design where practical
- Async APIs where appropriate
- CancellationToken support where appropriate
- XML documentation on public contracts
- Defensive coding
- No placeholder pseudo-code unless clearly marked