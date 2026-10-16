using System.Text.Json;

namespace AgentSession.MCP.Contracts;

public sealed record ResumeEntry(string Kind, JsonElement Value);

public sealed record SessionResumePage(
    string SnapshotId,
    long Sequence,
    long AcknowledgedSequence,
    IReadOnlyList<ResumeEntry> Entries,
    IReadOnlyList<string> Unknown,
    bool HasMore,
    string? Cursor
);
