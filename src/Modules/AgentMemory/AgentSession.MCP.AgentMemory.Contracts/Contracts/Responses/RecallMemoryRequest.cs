using System.ComponentModel.DataAnnotations;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Contracts;

public sealed class RecallMemoryRequest
{
    public string Query { get; set; } = string.Empty;
    [MinLength(1), MaxLength(3)]
    public List<MemoryTier> Tiers { get; set; } =
    [MemoryTier.Temp, MemoryTier.Short, MemoryTier.Long];
    public string? SessionId { get; set; }
    public string? TaskId { get; set; }
    public string? ParentStepId { get; set; }
    public string? AgentId { get; set; }
    public string? Category { get; set; }
    [MaxLength(100)]
    public List<string> Categories { get; set; } = [];
    public string? DecisionArea { get; set; }
    [MaxLength(100)]
    public List<string> DecisionAreas { get; set; } = [];
    [MaxLength(100)]
    public List<string> Tags { get; set; } = [];
    [MaxLength(10)]
    public List<MemoryState> Statuses { get; set; } = [];
    [MaxLength(100)]
    public List<string> MemoryIds { get; set; } = [];
    [Range(1, 100)]
    public int MaxResults { get; set; } = 10;
    [Range(-1d, 1d)]
    public double? MinimumSimilarity { get; set; }
    public bool IncludeCandidates { get; set; }
    public bool IncludeHistory { get; set; }
    public bool IncludeArchived { get; set; }
    public bool IncludeRetired { get; set; }
    public DateTimeOffset? SinceUtc { get; set; }
    public DateTimeOffset? BeforeUtc { get; set; }
}
