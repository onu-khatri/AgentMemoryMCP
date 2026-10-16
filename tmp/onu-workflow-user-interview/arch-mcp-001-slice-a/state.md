# ARCH-MCP-001 kickoff interview state

## Confirmed facts

- User-provided: Slice A is a representative read capability grounded in the memory/session domain.
- User-provided: Slice A implements `resume_agent_session`; `memory_recall` is a separate follow-on Slice B.
- User-provided: Use a pluggable JWT bearer model with a local test issuer.
- User-provided: Authorize with client ID and scopes; repository ID is the resource boundary; no tenant model for Slice A.
- User-provided: Deploy and verify Slice A with Docker Compose in local/CI environments.
- User-provided: Use the existing canonical repository-scoped filesystem as durable state; no new database, cache, queue, or task store for Slice A.
- Evidence-backed: Current repository targets .NET 10, uses MCP SDK 2.2.0, and uses OnuObservability packages 0.1.0-alpha.10.

## Remaining gaps

- Proposal must define the exact first-slice contract, architecture boundaries, authorization policies, Compose verification, and test gates.
- Production identity provider and cloud/Kubernetes deployment remain deferred beyond Slice A.

## Current branch

- Delivery: ARCH-MCP-001 / GitHub issue #1
- Slice: A — `resume_agent_session`
- Next action: create the capability delta/spec and design artifacts, validate the complete proposal, then request explicit implementation approval before any worktree or code changes.
