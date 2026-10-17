namespace AgentSession.MCP.Contracts;

public sealed record ActivateSharedSessionRequest(string SessionId, string ActorId, string OperationId);
public sealed record ResumeSessionRequest(string SessionId, string ActorId, int PageSize = 50, string? Cursor = null);
public sealed record CheckpointSessionRequest(string SessionId, string ActorId, string SnapshotId, long Sequence);
public sealed record ListSharedSessionsRequest(int PageSize = 50, string? After = null);
public sealed record SharedSessionActivation(string SessionId, long Sequence);
public sealed record SharedSessionList(IReadOnlyList<string> SessionIds, bool HasMore, string? After);
