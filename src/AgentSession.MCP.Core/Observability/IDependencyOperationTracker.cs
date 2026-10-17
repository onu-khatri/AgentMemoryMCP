namespace AgentSession.MCP.Core.Observability;

public interface IDependencyOperationTracker
{
    Task TrackAsync(
        DependencyOperation operation,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken
    );

    Task<T> TrackAsync<T>(
        DependencyOperation operation,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        Func<T, DependencyOperationMetadata?>? successMetadata = null
    );
}
