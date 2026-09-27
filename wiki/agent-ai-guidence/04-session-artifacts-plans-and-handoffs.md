# Session artifacts, plans and handoffs

[Wiki index](README.md)

`append_agent_memory` writes structured, revision-controlled artifacts inside one shared session. Despite its name, it is the session-artifact write path; it does not write reusable learning tiers.

Supported kinds are `context`, `decision`, `finding`, `handoff`, `plan` and `output`.

## Artifact design

Every artifact has a stable ID, kind, actor, title, optional task association, structured `data`, evidence references and a revision. The reserved artifact ID `context` is the only context artifact and uses the typed `context` object rather than `data`.

Use stable semantic IDs:

- `context`
- `decision-use-outbox`
- `finding-webhook-timeout`
- `plan-payments-v2`
- `output-webhook-test-report`
- `handoff-api-to-test-agent`

Avoid IDs based only on timestamps. Stable IDs make revisions and references intelligible.

## Creating multiple plans and selecting the current plan

Multiple plan artifacts may coexist. The shared context's `planReferences` explicitly selects the current plan or plans. Plan status is not a server-enforced lifecycle; if useful, record a documented convention such as `draft`, `active`, `completed`, `superseded` or `abandoned` inside `data`.

The safest replacement is one atomic batch:

```json
{
  "sessionId": "payments-migration",
  "actorId": "planning-agent",
  "operationId": "replace-plan-v1-with-v2",
  "updates": [
    {
      "expectedRevision": 3,
      "artifact": {
        "id": "plan-payments-v1",
        "kind": "plan",
        "actorId": "planning-agent",
        "title": "Payments migration plan v1",
        "data": {
          "status": "superseded",
          "supersededBy": "plan-payments-v2",
          "acceptanceCriteria": ["Existing payment flows remain compatible"],
          "steps": ["Preserved full v1 plan content here"],
          "validation": ["dotnet test"]
        },
        "evidence": ["decision:decision-provider-cutover"]
      }
    },
    {
      "expectedRevision": 0,
      "artifact": {
        "id": "plan-payments-v2",
        "kind": "plan",
        "actorId": "planning-agent",
        "title": "Payments migration plan v2",
        "data": {
          "status": "active",
          "acceptanceCriteria": ["Old and new webhook signatures pass"],
          "steps": ["Add dual verification", "Migrate callers", "Remove old key after approval"],
          "constraints": ["No breaking API change"],
          "validation": ["Run integration tests against both signatures"]
        },
        "evidence": ["finding:webhook-compatibility"]
      }
    },
    {
      "expectedRevision": 5,
      "artifact": {
        "id": "context",
        "kind": "context",
        "actorId": "planning-agent",
        "title": "Shared context",
        "context": {
          "objective": "Complete the payments migration",
          "planReferences": ["plan-payments-v2"],
          "constraints": ["No breaking API change"],
          "nextActions": ["Create and claim the dual-signature task"]
        },
        "evidence": []
      }
    }
  ]
}
```

Updates replace the complete artifact representation. Resume and preserve existing fields before submitting a replacement. `expectedRevision: 0` creates; a positive expected revision updates. If another agent changes an artifact first, reconcile the conflict and use a new operation ID for the changed request.

Artifact kind and task association cannot change after creation. A task-linked artifact also needs the current `claimToken` in its update wrapper.

## Decisions, findings and outputs

- A **decision** records what was chosen, alternatives, rationale and evidence.
- A **finding** records an observed fact, scope, reproduction and uncertainty.
- An **output** points to a concrete deliverable such as a file, commit, report or dataset.
- A **handoff** tells the next agent what is complete, what remains, blockers and the next safe action.

Do not represent task ownership or completion in these artifacts. Use `coordinate_agent_task` for task state and artifacts for supporting information.

## Real-life example: design agent hands work to an executor

The design agent saves `plan-rate-limit-v3`, updates `context.planReferences`, creates a decision explaining the chosen token-bucket algorithm, and creates tasks through the coordination tool. The executor resumes, reads the exact plan and decision, claims the implementation task and does not rerun the architectural comparison. When finished, it saves an output artifact referencing changed files and test results, completes the task with the same evidence, and writes a handoff for the reviewer.

