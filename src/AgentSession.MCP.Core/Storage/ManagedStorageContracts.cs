namespace AgentSession.MCP.Core.Storage;

public enum ManagedArea
{
    Session,
    Learning,
}

public sealed record ManagedFile(ManagedArea Area, string? SessionId, string[] Parts);

public sealed record ManagedMutation(
    ManagedFile File,
    string? Content,
    string? ExpectedHash,
    DateTimeOffset? ExpiresAtUtc = null,
    string? BinaryContentBase64 = null
);

public sealed record ManagedCommit(
    string OperationId,
    string RequestHash,
    List<ManagedMutation> Mutations
);

public sealed record ManagedReceipt(string OperationId, string RequestHash);

public sealed record ManagedDocument(ManagedFile File, string Content);

public interface IManagedStorageContext
{
    string Root { get; }
    string RepositoryId { get; }
    string SessionsRoot { get; }
    string LearningRoot { get; }
    string VectorRoot { get; }
    string LocksRoot { get; }
}

public interface IManagedStoragePathResolver
{
    string SessionFile(string repositoryId, string sessionId, params string[] relativeParts);
    string LearningFile(string repositoryId, params string[] relativeParts);
    string RepositoryLockFile();
    string ApprovalFile(string memoryId, long revision, string action);
}

public interface IManagedTransactionStore
{
    Task<int> GetRecoveryBacklogAsync(CancellationToken cancellationToken = default);

    Task<string?> ReadAsync(
        ManagedFile file,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<string?>> ReadManyAsync(
        IReadOnlyList<ManagedFile> files,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<string>> ListSessionIdsAsync(
        int count,
        string? after,
        CancellationToken cancellationToken = default
    );

    Task<ManagedReceipt> CommitAsync(
        string operationId,
        IReadOnlyList<ManagedMutation> mutations,
        CancellationToken cancellationToken = default,
        Action? validateCommit = null
    );

    Task<IReadOnlyList<ManagedDocument>> ScanLearningRecordsAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ManagedDocument>> ScanLearningDirectoryAsync(
        IReadOnlyList<string> directoryParts,
        int maxResults,
        CancellationToken cancellationToken = default
    );
}
