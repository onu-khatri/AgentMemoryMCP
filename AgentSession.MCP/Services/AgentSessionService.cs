using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Models;
using AgentSession.MCP.Tools;
using Microsoft.Extensions.Logging;

namespace AgentSession.MCP.Services;

public sealed class AgentSessionService : IAgentSessionService
{
    private static readonly HashSet<string> LogLevels = ["Info", "Warning", "Error", "Debug"];
    private readonly IAgentSessionStore _store;
    private readonly IYamlSerializer _yamlSerializer;
    private readonly SessionStoragePathBuilder _pathBuilder;
    private readonly ILogger<AgentSessionService> _logger;

    public AgentSessionService(
        IAgentSessionStore store,
        IYamlSerializer yamlSerializer,
        SessionStoragePathBuilder pathBuilder,
        ILogger<AgentSessionService> logger)
    {
        _store = store;
        _yamlSerializer = yamlSerializer;
        _pathBuilder = pathBuilder;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<ListAgentSessionsItem>> ListAgentSessionsAsync(CancellationToken cancellationToken)
    {
        var sessions = await _store.ListSessionsAsync(cancellationToken);
        return sessions
            .Select(item => new ListAgentSessionsItem
            {
                SessionId = item.SessionId,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                CurrentStateSummary = item.CurrentStateSummary,
                ArtifactCount = item.ArtifactCount,
                LastLogTimestamp = item.LastLogTimestamp,
                Description = item.Description
            })
            .ToList();
    }

    public async Task<string> CreateOrActivateSessionAsync(CreateOrActivateSessionRequest request, CancellationToken cancellationToken)
    {
        var (session, created, paths) = await _store.CreateOrActivateSessionAsync(
            request.SessionId,
            request.InitialState,
            request.AgentName,
            cancellationToken);

        var payload = new
        {
            session_id = session.SessionId,
            session_path = paths.SessionPath,
            created_or_activated = created ? "created" : "activated",
            current_state = session.CurrentState,
            usage_instruction = session.UsageInstruction,
            available_files = new[]
            {
                AgentSessionState.AgentSessionStateFileName,
                AgentSessionState.AgentMemoryFileName,
                AgentSessionState.AgentSessionLogFileName,
                "artifacts/"
            },
            artifact_folder_path = paths.ArtifactDirectoryPath
        };

        return _yamlSerializer.Serialize(payload);
    }

    public async Task<ReadAgentMemoryResponse> ReadAgentMemoryAsync(ReadAgentMemoryRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);

        var memory = await _store.ReadMemoryAsync(sessionId, cancellationToken);
        var artifacts = await _store.ListArtifactsAsync(sessionId, cancellationToken);
        var logs = await _store.ReadLogsAsync(sessionId, Math.Clamp(request.LatestLogEntries, 1, 200), cancellationToken);
        var state = await _store.GetSessionStateAsync(sessionId, cancellationToken);

        return new ReadAgentMemoryResponse
        {
            SessionId = sessionId,
            Memory = memory,
            Artifacts = artifacts.Select(ToArtifactItem).ToList(),
            LatestLogs = logs.Select(log => new LogItem
            {
                Timestamp = log.Timestamp,
                Message = log.Message,
                AgentName = log.AgentName,
                Level = log.Level,
                EventType = log.EventType,
                CorrelationId = log.CorrelationId
            }).ToList(),
            Metadata = new SessionMetadataItem
            {
                CreatedAt = state.CreatedAt,
                UpdatedAt = state.UpdatedAt,
                AgentName = state.AgentName,
                CurrentState = state.CurrentState,
                Description = state.Description
            }
        };
    }

    public async Task<AppendAgentMemoryResponse> AppendAgentMemoryAsync(AppendAgentMemoryRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ValidationException("content is required.");
        }

        var size = await _store.AppendMemoryAsync(sessionId, request.Content, request.AgentName, request.SectionTitle, cancellationToken);
        return new AppendAgentMemoryResponse
        {
            Success = true,
            Message = "Memory updated successfully.",
            SessionId = sessionId,
            MemorySizeBytes = size
        };
    }

    public async Task<OperationResult> LogAgentEventAsync(LogAgentEventRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ValidationException("message is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AgentName))
        {
            throw new ValidationException("agentName is required.");
        }

        var level = string.IsNullOrWhiteSpace(request.Level) ? "Info" : request.Level.Trim();
        if (!LogLevels.Contains(level))
        {
            throw new ValidationException("level must be one of Info, Warning, Error, Debug.");
        }

        var entry = new AgentSessionLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Message = request.Message.Trim(),
            AgentName = request.AgentName.Trim(),
            Level = level,
            EventType = request.EventType?.Trim(),
            CorrelationId = request.CorrelationId?.Trim()
        };

        await _store.AppendLogAsync(sessionId, entry, cancellationToken);
        _logger.LogInformation("Logged event for session {SessionId}: {Message}", sessionId, entry.Message);

        return new OperationResult
        {
            Success = true,
            Message = "Log entry persisted."
        };
    }

    public async Task<CreateAgentArtifactResponse> CreateAgentArtifactAsync(CreateAgentArtifactRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);
        if (string.IsNullOrWhiteSpace(request.ArtifactName))
        {
            throw new ValidationException("artifactName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ValidationException("description is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IntendedUse))
        {
            throw new ValidationException("intendedUse is required.");
        }

        var doc = await _store.CreateOrUpdateArtifactAsync(
            sessionId,
            request.ArtifactName,
            request.Description,
            request.IntendedUse,
            request.Content,
            request.ContentType,
            request.Tags,
            request.Overwrite,
            cancellationToken);

        return new CreateAgentArtifactResponse
        {
            Success = true,
            Message = "Artifact created successfully.",
            FilePath = Path.Combine(_pathBuilder.Build(sessionId).ArtifactDirectoryPath, doc.Metadata.FileName),
            Metadata = ToArtifactItem(doc.Metadata)
        };
    }

    public async Task<ReadAgentArtifactResponse> ReadAgentArtifactAsync(ReadAgentArtifactRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);
        if (string.IsNullOrWhiteSpace(request.ArtifactName))
        {
            throw new ValidationException("artifactName is required.");
        }

        var artifact = await _store.ReadArtifactAsync(sessionId, request.ArtifactName, cancellationToken);
        return new ReadAgentArtifactResponse
        {
            Metadata = ToArtifactItem(artifact.Metadata),
            Content = artifact.Content
        };
    }

    public async Task<ListAgentArtifactsResponse> ListAgentArtifactsAsync(ListAgentArtifactsRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);
        var artifacts = await _store.ListArtifactsAsync(sessionId, cancellationToken);

        return new ListAgentArtifactsResponse
        {
            SessionId = sessionId,
            Artifacts = artifacts.Select(ToArtifactItem).ToList()
        };
    }

    public async Task<SaveFinalPlanResponse> SaveFinalPlanAsync(SaveFinalPlanRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);
        if (string.IsNullOrWhiteSpace(request.PlanContent))
        {
            throw new ValidationException("planContent is required.");
        }

        var plan = await _store.SaveFinalPlanAsync(
            sessionId,
            request.PlanContent,
            request.PlanTitle,
            request.AgentName,
            ToFinalPlanDetails(request),
            cancellationToken);

        return new SaveFinalPlanResponse
        {
            Success = true,
            SessionId = sessionId,
            ArtifactName = plan.Metadata.Name,
            FileName = plan.Metadata.FileName,
            SavedAt = plan.Metadata.UpdatedAt
        };
    }

    public async Task<GetLatestFinalPlanResponse> GetLatestFinalPlanAsync(GetLatestFinalPlanRequest request, CancellationToken cancellationToken)
    {
        var sessionId = ValidateSessionId(request.SessionId);
        var latest = await _store.GetLatestFinalPlanAsync(sessionId, cancellationToken);
        if (latest is null)
        {
            return new GetLatestFinalPlanResponse
            {
                Success = false,
                NotFound = true,
                Message = "No final plan exists for this session.",
                SessionId = sessionId,
                Metadata = null,
                PlanContent = null
            };
        }

        return new GetLatestFinalPlanResponse
        {
            Success = true,
            NotFound = false,
            Message = "Latest final plan retrieved successfully.",
            SessionId = sessionId,
            Metadata = ToArtifactItem(latest.Metadata),
            PlanContent = latest.Content,
            PlanDetails = ToFinalPlanDetailsItem(latest.Metadata.FinalPlanDetails)
        };
    }

    private static FinalPlanDetails? ToFinalPlanDetails(SaveFinalPlanRequest request)
    {
        var details = new FinalPlanDetails
        {
            Constraints = request.Constraints is null ? null : new FinalPlanConstraints
            {
                ProjectKnowledge = CleanList(request.Constraints.ProjectKnowledge),
                ArchitecturalConstraints = CleanList(request.Constraints.ArchitecturalConstraints)
            },
            Assumptions = CleanList(request.Assumptions),
            ImpactAnalysis = request.ImpactAnalysis is null ? null : new FinalPlanImpactAnalysis
            {
                AffectedModuleComponents = CleanList(request.ImpactAnalysis.AffectedModuleComponents),
                CrossCuttingConcerns = CleanList(request.ImpactAnalysis.CrossCuttingConcerns),
                PotentialSideEffects = CleanList(request.ImpactAnalysis.PotentialSideEffects)
            },
            ImplementationStrategy = request.ImplementationStrategy is null ? null : new FinalPlanImplementationStrategy
            {
                Summary = request.ImplementationStrategy.Summary?.Trim(),
                Approach = CleanList(request.ImplementationStrategy.Approach)
            },
            StepByStepPlan = request.StepByStepPlan is null ? null : new FinalPlanStepByStep
            {
                Notes = CleanList(request.StepByStepPlan.Notes),
                Steps = request.StepByStepPlan.Steps
                    .Where(step => !string.IsNullOrWhiteSpace(step.Title))
                    .Select(step => new FinalPlanStep
                    {
                        Title = step.Title.Trim(),
                        Note = step.Note?.Trim()
                    })
                    .ToList()
            },
            TechnicalRisks = request.TechnicalRisks?
                .Where(risk => !string.IsNullOrWhiteSpace(risk.Risk))
                .Select(risk => new FinalPlanRisk
                {
                    Risk = risk.Risk.Trim(),
                    Mitigation = risk.Mitigation?.Trim()
                })
                .ToList() ?? [],
            OpenQuestions = CleanList(request.OpenQuestions),
            RequiredButSkippedDecisions = CleanList(request.RequiredButSkippedDecisions),
            Approval = request.Approval is null
                ? null
                : new FinalPlanApproval
                {
                    IsApproved = request.Approval.IsApproved,
                    ApprovalTimestampUtc = request.Approval.ApprovalTimestamp?.ToUniversalTime().ToString("O")
                }
        };

        var hasData = details.Constraints is not null
            || details.Assumptions.Count > 0
            || details.ImpactAnalysis is not null
            || details.ImplementationStrategy is not null
            || details.StepByStepPlan is not null
            || details.TechnicalRisks.Count > 0
            || details.OpenQuestions.Count > 0
            || details.RequiredButSkippedDecisions.Count > 0
            || details.Approval is not null;

        return hasData ? details : null;
    }

    private static FinalPlanDetailsItem? ToFinalPlanDetailsItem(FinalPlanDetails? details)
    {
        if (details is null)
        {
            return null;
        }

        return new FinalPlanDetailsItem
        {
            Constraints = details.Constraints is null ? null : new PlanConstraintsItem
            {
                ProjectKnowledge = details.Constraints.ProjectKnowledge,
                ArchitecturalConstraints = details.Constraints.ArchitecturalConstraints
            },
            Assumptions = details.Assumptions,
            ImpactAnalysis = details.ImpactAnalysis is null ? null : new PlanImpactAnalysisItem
            {
                AffectedModuleComponents = details.ImpactAnalysis.AffectedModuleComponents,
                CrossCuttingConcerns = details.ImpactAnalysis.CrossCuttingConcerns,
                PotentialSideEffects = details.ImpactAnalysis.PotentialSideEffects
            },
            ImplementationStrategy = details.ImplementationStrategy is null ? null : new PlanImplementationStrategyItem
            {
                Summary = details.ImplementationStrategy.Summary,
                Approach = details.ImplementationStrategy.Approach
            },
            StepByStepPlan = details.StepByStepPlan is null ? null : new PlanStepByStepItem
            {
                Notes = details.StepByStepPlan.Notes,
                Steps = details.StepByStepPlan.Steps.Select(step => new PlanStepItem
                {
                    Title = step.Title,
                    Note = step.Note
                }).ToList()
            },
            TechnicalRisks = details.TechnicalRisks.Select(risk => new PlanRiskItem
            {
                Risk = risk.Risk,
                Mitigation = risk.Mitigation
            }).ToList(),
            OpenQuestions = details.OpenQuestions,
            RequiredButSkippedDecisions = details.RequiredButSkippedDecisions,
            Approval = details.Approval is null ? null : new PlanApprovalItem
            {
                IsApproved = details.Approval.IsApproved,
                ApprovalTimestamp = ParseUtcTimestamp(details.Approval.ApprovalTimestampUtc)
            }
        };
    }

    private static List<string> CleanList(IReadOnlyCollection<string>? values)
        => values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];

    private static DateTimeOffset? ParseUtcTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static ArtifactItem ToArtifactItem(AgentArtifactMetadata metadata)
        => new()
        {
            Name = metadata.Name,
            Description = metadata.Description,
            IntendedUse = metadata.IntendedUse,
            ContentType = metadata.ContentType,
            CreatedAt = metadata.CreatedAt,
            UpdatedAt = metadata.UpdatedAt,
            Tags = metadata.Tags,
            FileName = metadata.FileName
        };

    private static string ValidateSessionId(string sessionId)
    {
        var sanitized = NameSanitizer.SanitizeIdentifier(sessionId);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ValidationException("sessionId is required and must be filesystem-safe.");
        }

        return sanitized;
    }
}
