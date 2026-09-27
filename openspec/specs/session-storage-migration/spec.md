## Purpose

Provide central session storage that fits resumption and coordination while retaining existing behavior only when useful and protecting historical data.

## Requirements
### Requirement: Requirement-driven reuse
Existing session tools, payloads, formats and dependencies SHALL be retained only when they support the new session or learning requirements. Delivery SHALL document a retain/adapt/remove mapping with requirement and verification references. Blanket backward compatibility SHALL NOT be a release condition.

#### Scenario: Obsolete freeform append behavior
- **WHEN** an old payload cannot express required structured coordination updates
- **THEN** the server rejects it clearly or supports an explicitly justified adapter rather than silently inventing structured state

### Requirement: Central repository-scoped sessions
New sessions SHALL use `%USERPROFILE%/.codex/AgentMemory/sessions/<repository-id>/<session-id>` and structured durable state. Discovery and access SHALL be scoped to the bound repository. Required plan, artifact and evidence capabilities SHALL remain available through contracts designed for the new workflows, without requiring all original tool names or formats.

#### Scenario: Resume new structured session
- **WHEN** an agent resumes after process restart
- **THEN** the current session state and plan/output references are available within the bound repository

### Requirement: Historical data remains untouched
Startup and upgrade SHALL NOT automatically copy, rewrite, delete or import historical session files. Automated legacy migration SHALL be outside this change unless a concrete requirement is separately confirmed. Documentation SHALL explain new contracts/paths, unsupported configuration and rollback limitations.

#### Scenario: Old sessions exist on the machine
- **WHEN** the new server starts with central storage configured
- **THEN** it operates on the configured new session store without modifying historical files or claiming they were migrated
