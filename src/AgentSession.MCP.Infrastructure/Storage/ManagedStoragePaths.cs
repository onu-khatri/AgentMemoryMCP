using AgentSession.MCP.Core.Storage;
using AgentSession.MCP.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

public sealed class ManagedStoragePaths : IManagedStorageContext
{
    public ManagedStoragePaths(
        IOptions<SystemStorageOptions> storage,
        IOptions<RepositoryOptions> repository
    )
    {
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(storage.Value.Root));
        RepositoryId = repository.Value.Id;
        SessionsRoot = Path.Combine(Root, "sessions", RepositoryId);
        LearningRoot = Path.Combine(Root, "repositories", RepositoryId, "AiLearning");
        VectorRoot = Path.Combine(Root, ".vector", "qdrant");
        LocksRoot = Path.Combine(Root, ".locks");
    }

    public string Root { get; }
    public string RepositoryId { get; }
    public string SessionsRoot { get; }
    public string LearningRoot { get; }
    public string VectorRoot { get; }
    public string LocksRoot { get; }
}
