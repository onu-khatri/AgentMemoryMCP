# Production source catalog

[Previous: Testing](14-testing-and-acceptance.md) | [Developer wiki](README.md) | [Next: Test and operations catalog](16-test-and-operations-source-catalog.md)

This catalog identifies the responsibility of every production source area. Open the linked file, then use the named classes and methods as navigation anchors.

## Process and package

| File | Key code | Responsibility |
| --- | --- | --- |
| [Program.cs](../../AgentSession.MCP/Program.cs) | top-level program | Generic Host, stderr logging, hosted maintenance, stdio MCP and tool registration |
| [AgentSession.MCP.csproj](../../AgentSession.MCP/AgentSession.MCP.csproj) | project properties | .NET 10 executable/MCP tool packaging, runtime identifiers and pinned dependencies |
| [.mcp/server.json](../../AgentSession.MCP/.mcp/server.json) | MCP package manifest | stdio entry command and server description |
| [AssemblyInfo.cs](../../AgentSession.MCP/Properties/AssemblyInfo.cs) | assembly attributes | test access to internal fault/recovery types |

## Tool layer

| File | Class/methods | Responsibility |
| --- | --- | --- |
| [SharedSessionTools.cs](../../AgentSession.MCP/Tools/SharedSessionTools.cs) | `SharedSessionTools`, six snake_case methods, `Invoke` | MCP metadata/descriptions and exception translation for session tools |
| [MemoryTools.cs](../../AgentSession.MCP/Tools/MemoryTools.cs) | `MemoryTools`, twenty snake_case methods, `Invoke` | MCP metadata/descriptions and exception translation for learning tools |

## Configuration and options

| File | Class/methods | Responsibility |
| --- | --- | --- |
| [MemoryConfigurationExtensions.cs](../../AgentSession.MCP/Extensions/MemoryConfigurationExtensions.cs) | `AddMemoryStorageConfiguration`, validation helpers | Bind/validate options and construct service graph |
| [SystemStorageOptions.cs](../../AgentSession.MCP/Options/SystemStorageOptions.cs) | `SystemStorageOptions` | Central root default |
| [RepositoryOptions.cs](../../AgentSession.MCP/Options/RepositoryOptions.cs) | `RepositoryOptions` | Required stable repository identity |
| [SessionCoordinationOptions.cs](../../AgentSession.MCP/Options/SessionCoordinationOptions.cs) | `SessionCoordinationOptions` | Claim and snapshot lifetimes |
| [MemoryPolicyOptions.cs](../../AgentSession.MCP/Options/MemoryPolicyOptions.cs) | memory, limits, embedding, Ollama and Qdrant option classes | Retention, bounds and dependency configuration |

## Session contracts and models

| File | Types | Responsibility |
| --- | --- | --- |
| [SessionResumeRequests.cs](../../AgentSession.MCP/Contracts/Requests/SessionResumeRequests.cs) | activation/list/resume/checkpoint requests and simple results | Session lifecycle wire contracts |
| [SessionCoordinationRequests.cs](../../AgentSession.MCP/Contracts/Requests/SessionCoordinationRequests.cs) | `UpdateSessionArtifactsRequest`, `SessionArtifactUpdate` | Atomic artifact batch contract |
| [CoordinateTaskRequest.cs](../../AgentSession.MCP/Contracts/Requests/CoordinateTaskRequest.cs) | `CoordinateTaskRequest` | All task actions in one typed contract |
| [SharedSessionArtifact.cs](../../AgentSession.MCP/Models/SharedSessionArtifact.cs) | `SharedSessionArtifact` | Versioned artifact record |
| [SessionWorkingContext.cs](../../AgentSession.MCP/Models/SessionWorkingContext.cs) | `SessionWorkingContext` | Reserved typed context fields |
| [SessionCoordination.cs](../../AgentSession.MCP/Models/SessionCoordination.cs) | artifact/task enums, `SessionChange`, `AgentCheckpoint` | Shared coordination vocabulary |
| [CoordinatedTask.cs](../../AgentSession.MCP/Models/CoordinatedTask.cs) | `CoordinatedTask` | Persisted task, lease and evidence state |
| [SessionCoordinationMetadata.cs](../../AgentSession.MCP/Models/SessionCoordinationMetadata.cs) | `SessionCoordinationMetadata` | Session root metadata |

## Memory contracts and models

| File | Types | Responsibility |
| --- | --- | --- |
| [RememberMemoryRequest.cs](../../AgentSession.MCP/Contracts/Requests/RememberMemoryRequest.cs) | `RememberMemoryRequest` | Canonical creation fields and provenance |
| [MemoryRequests.cs](../../AgentSession.MCP/Contracts/Requests/MemoryRequests.cs) | get/update/archive/compaction/outcome/lifecycle/review requests | Main mutation and read contracts |
| [MemoryOperationsRequests.cs](../../AgentSession.MCP/Contracts/Requests/MemoryOperationsRequests.cs) | event, reindex and status contracts | Operations/health wire contracts |
| [MemoryWriteResult.cs](../../AgentSession.MCP/Contracts/Requests/MemoryWriteResult.cs) | `MemoryWriteResult` | Common mutation response |
| [RecallMemoryRequest.cs](../../AgentSession.MCP/Contracts/Responses/RecallMemoryRequest.cs) | `RecallMemoryRequest` | Unified search/filter contract |
| [RecallMemoryResult.cs](../../AgentSession.MCP/Contracts/Responses/RecallMemoryResult.cs) | `RecallMemoryResult` | Search results, mode and health reasons |
| [MemoryReadResult.cs](../../AgentSession.MCP/Contracts/Responses/MemoryReadResult.cs) | `MemoryReadResult` | Exact record availability result |
| [ArchiveMemoryResult.cs](../../AgentSession.MCP/Contracts/Responses/ArchiveMemoryResult.cs) | `ArchiveMemoryResult` | Verified archive outcome |
| [CleanupMemoryResult.cs](../../AgentSession.MCP/Contracts/Responses/CleanupMemoryResult.cs) | `CleanupMemoryResult` | Maintenance counts |
| [MemoryRecord.cs](../../AgentSession.MCP/Models/Memory/MemoryRecord.cs) | tiers/states/outcomes/relationships and `MemoryRecord` | Canonical learning aggregate |
| [MemoryProvenance.cs](../../AgentSession.MCP/Models/Memory/MemoryProvenance.cs) | provenance model | Immutable origin and action history |
| [MemoryOutcome.cs](../../AgentSession.MCP/Models/Memory/MemoryOutcome.cs) | outcome and confirmation models | Evidence-backed experience |
| [MemoryEvent.cs](../../AgentSession.MCP/Models/Memory/MemoryEvent.cs) | event and journal metadata | Sanitized append-oriented audit/observation events |
| [CompactionDetails.cs](../../AgentSession.MCP/Models/Memory/CompactionDetails.cs) | compaction kind/details/coverage | Traceable compact representations |
| [EmbeddingMetadata.cs](../../AgentSession.MCP/Models/Memory/EmbeddingMetadata.cs) | embedding metadata | Provider/model/version/dimension identity on records |
| [VectorPendingWork.cs](../../AgentSession.MCP/Models/Memory/VectorPendingWork.cs) | pending vector work | Durable retry/backoff state |
| [VectorMigration.cs](../../AgentSession.MCP/Models/Memory/VectorMigration.cs) | active collection and migration intent models | Crash-safe collection migration |

## Storage and validation services

| File | Class/methods | Responsibility |
| --- | --- | --- |
| [MemoryStoragePaths.cs](../../AgentSession.MCP/Services/MemoryStoragePaths.cs) | `MemoryStoragePaths` | Derive configured roots once |
| [ManagedStoragePathResolver.cs](../../AgentSession.MCP/Services/ManagedStoragePathResolver.cs) | `SessionFile`, `LearningFile`, `ApprovalFile`, `RequireIdentifier` | Safe operation-time path resolution |
| [RepositoryMutationLock.cs](../../AgentSession.MCP/Services/RepositoryMutationLock.cs) | `AcquireAsync` | In-process and cross-process repository lease |
| [SystemFileSystem.cs](../../AgentSession.MCP/Services/SystemFileSystem.cs) | atomic text/byte/write methods | Durable single-file staging and replace |
| [ManagedTransactionStore.cs](../../AgentSession.MCP/Services/ManagedTransactionStore.cs) | read/scan/commit/recover/apply methods | Locked roll-forward multi-file transaction engine |
| [LearningCatalog.cs](../../AgentSession.MCP/Services/LearningCatalog.cs) | `ReadAsync`, `RebuildAsync`, `EntryFor`, `FileFor`, `Update` | Sharded derived canonical index and layout mapping |
| [MemoryJson.cs](../../AgentSession.MCP/Services/MemoryJson.cs) | `Options`, `SchemaFor` | Strict shared JSON and schema generation |
| [MemoryRequestValidator.cs](../../AgentSession.MCP/Services/MemoryRequestValidator.cs) | validation methods | Serialized byte/depth/batch bounds and content policy |
| [MemoryRecordValidator.cs](../../AgentSession.MCP/Services/MemoryRecordValidator.cs) | `Validate` | Persisted aggregate invariants |
| [MemoryContentPolicy.cs](../../AgentSession.MCP/Services/MemoryContentPolicy.cs) | `Validate`, recursive inspectors | Secret-pattern rejection |
| [IMemoryContentPolicy.cs](../../AgentSession.MCP/Interfaces/IMemoryContentPolicy.cs) | interface | Injectable content-policy boundary |
| [MemoryClaimHash.cs](../../AgentSession.MCP/Services/MemoryClaimHash.cs) | `Content`, `Scope` | Normalized deduplication hashes |
| [OperatorGrantVerifier.cs](../../AgentSession.MCP/Services/OperatorGrantVerifier.cs) | `Require` | External revision/action-scoped authorization |
| [NameSanitizer.cs](../../AgentSession.MCP/Helpers/NameSanitizer.cs) | `IsSafePathSegment` | Stable lowercase identifier grammar |
| [ValidationException.cs](../../AgentSession.MCP/Helpers/ValidationException.cs) | `ValidationException` | Domain error plus stable code |

## Session services

| File | Class/methods | Responsibility |
| --- | --- | --- |
| [SharedSessionLifecycleService.cs](../../AgentSession.MCP/Services/SharedSessionLifecycleService.cs) | `ActivateAsync`, `ListAsync` | Session creation/activation and bounded discovery |
| [SessionCoordinationService.cs](../../AgentSession.MCP/Services/SessionCoordinationService.cs) | `UpdateArtifactsAsync`, `CoordinateAsync`, `CoordinateCoreAsync` | Artifact concurrency and task state machine |
| [SessionResumeService.cs](../../AgentSession.MCP/Services/SessionResumeService.cs) | `ResumeAsync`, `CheckpointAsync`, chunk helpers | Consistent snapshots, pagination and acknowledgement |

## Canonical learning service

`CanonicalMemoryService` is split by concern but compiled as one partial class:

| File | Main methods |
| --- | --- |
| [CanonicalMemoryService.cs](../../AgentSession.MCP/Services/CanonicalMemoryService.cs) | remember, get, temp update, cleanup, maintenance, canonical read/commit helpers |
| [CanonicalMemoryService.Status.cs](../../AgentSession.MCP/Services/CanonicalMemoryService.Status.cs) | status snapshot counting |
| [CanonicalMemoryService.Curation.cs](../../AgentSession.MCP/Services/CanonicalMemoryService.Curation.cs) | recall, review, promote, delete, supersede and retire |
| [CanonicalMemoryService.Compaction.cs](../../AgentSession.MCP/Services/CanonicalMemoryService.Compaction.cs) | prepare, commit and one-call compaction |
| [CanonicalMemoryService.Archive.cs](../../AgentSession.MCP/Services/CanonicalMemoryService.Archive.cs) | gzip archive construction, verification and archived reads |
| [CanonicalMemoryService.Events.cs](../../AgentSession.MCP/Services/CanonicalMemoryService.Events.cs) | authoritative event append and caller observation events |
| [CanonicalMemoryService.Outcomes.cs](../../AgentSession.MCP/Services/CanonicalMemoryService.Outcomes.cs) | outcomes, independence and confidence |

## Semantic and maintenance services

| File | Main classes/methods | Responsibility |
| --- | --- | --- |
| [EmbeddingProjection.cs](../../AgentSession.MCP/Services/EmbeddingProjection.cs) | projection, fingerprint, collection naming | Stable embedding identity |
| [OllamaEmbeddingClient.cs](../../AgentSession.MCP/Services/OllamaEmbeddingClient.cs) | identity and batch embed | Strict local provider adapter |
| [QdrantVectorIndex.cs](../../AgentSession.MCP/Services/QdrantVectorIndex.cs) | readiness, collection, upsert, state, delete, alias, search | Rebuildable repository-filtered vector index |
| [VectorIndexCoordinator.cs](../../AgentSession.MCP/Services/VectorIndexCoordinator.cs) | reconcile, process pending, defer | Canonical/vector convergence |
| [VectorMigrationService.cs](../../AgentSession.MCP/Services/VectorMigrationService.cs) | active identity, migrate, recover, verify, finalize | Versioned collection rebuild and alias switch |
| [MemoryReindexService.cs](../../AgentSession.MCP/Services/MemoryReindexService.cs) | `ReindexAsync` | Scope dispatcher and metrics |
| [MemoryMaintenanceService.cs](../../AgentSession.MCP/Services/MemoryMaintenanceService.cs) | worker, state and snapshot | Bounded periodic maintenance |
| [MemoryStatusService.cs](../../AgentSession.MCP/Services/MemoryStatusService.cs) | `GetAsync` | Aggregate safe operational health |

