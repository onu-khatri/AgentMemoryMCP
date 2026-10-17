using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Core.Storage;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Options;
using AgentSession.MCP.Services;
using AgentSession.MCP.Session.Contracts.Capabilities;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.UnitTests;

public sealed class SessionBehaviorTests
{
    [Fact]
    public async Task Context_mismatch_is_rejected_before_storage_access()
    {
        var store = new InMemoryStore();
        ISessionLifecycleCapabilities service = Lifecycle(store);

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.ActivateAsync(
            new("session", "actor", "operation"),
            Context("another-repository"),
            default
        ));

        Assert.Equal("repository_mismatch", error.Code);
        Assert.Equal(0, store.AccessCount);
    }

    [Fact]
    public async Task Activation_is_idempotent_and_persists_canonical_identity()
    {
        var store = new InMemoryStore();
        ISessionLifecycleCapabilities service = Lifecycle(store);
        var request = new ActivateSharedSessionRequest("session", "actor", "operation");

        var first = await service.ActivateAsync(request, Context(), default);
        var second = await service.ActivateAsync(request, Context(), default);

        Assert.Equal(first, second);
        Assert.Equal(1, first.Sequence);
        var json = Assert.Single(store.Documents).Value;
        using var document = JsonDocument.Parse(json);
        Assert.Equal("repo", document.RootElement.GetProperty("repositoryId").GetString());
        Assert.Equal("session", document.RootElement.GetProperty("sessionId").GetString());
    }

    [Fact]
    public async Task Cancellation_propagates_to_the_store()
    {
        var store = new InMemoryStore();
        ISessionLifecycleCapabilities service = Lifecycle(store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ActivateAsync(
            new("session", "actor", "operation"),
            Context(),
            cancellation.Token
        ));
    }

    [Fact]
    public void Validator_enforces_limits_depth_batch_and_sensitive_content()
    {
        var validator = new SessionRequestValidator(Microsoft.Extensions.Options.Options.Create(new SessionLimitsOptions
        {
            MaxContentBytes = 64,
            MaxRecordBytes = 256,
            MaxJsonDepth = 3,
            MaxMutationBatch = 2,
            MaxRecallResults = 5,
        }));

        validator.ValidateBatch(2);
        Assert.Throws<ValidationException>(() => validator.ValidateBatch(3));
        Assert.Throws<ValidationException>(() => validator.Validate(new { value = new string('x', 300) }));
        var secret = Assert.Throws<ValidationException>(() => validator.Validate(
            new { authorization = "Bearer abcdefghijklmnopqrstuvwxyz" },
            content: true
        ));
        Assert.Equal("content_policy_rejected", secret.Code);
        Assert.Throws<ValidationException>(() => validator.Validate(
            new { value = new { one = new { two = new { three = true } } } }
        ));
    }

    private static SharedSessionLifecycleService Lifecycle(InMemoryStore store) => new(
        store,
        new RepositoryResource("repo"),
        TimeProvider.System,
        Microsoft.Extensions.Options.Options.Create(new SessionLimitsOptions()),
        new SessionContextGuard(new RepositoryResource("repo"))
    );

    private static PlatformRequestContext Context(string repositoryId = "repo") => new(
        new ClientIdentity("unit-tests"),
        null,
        null,
        new HashSet<string>(StringComparer.Ordinal),
        new RepositoryResource(repositoryId),
        "unit-test"
    );

    private sealed class InMemoryStore : IManagedTransactionStore
    {
        public Dictionary<string, string> Documents { get; } = [];
        public int AccessCount { get; private set; }

        public Task<int> GetRecoveryBacklogAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<string?> ReadAsync(ManagedFile file, CancellationToken cancellationToken = default)
        {
            AccessCount++;
            cancellationToken.ThrowIfCancellationRequested();
            Documents.TryGetValue(Key(file), out var value);
            return Task.FromResult(value);
        }

        public Task<IReadOnlyList<string?>> ReadManyAsync(
            IReadOnlyList<ManagedFile> files,
            CancellationToken cancellationToken = default
        )
        {
            AccessCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<string?>>(files.Select(file =>
                Documents.TryGetValue(Key(file), out var value) ? value : null).ToArray());
        }

        public Task<IReadOnlyList<string>> ListSessionIdsAsync(
            int count,
            string? after,
            CancellationToken cancellationToken = default
        )
        {
            AccessCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        public Task<ManagedReceipt> CommitAsync(
            string operationId,
            IReadOnlyList<ManagedMutation> mutations,
            CancellationToken cancellationToken = default,
            Action? validateCommit = null
        )
        {
            AccessCount++;
            cancellationToken.ThrowIfCancellationRequested();
            validateCommit?.Invoke();
            foreach (var mutation in mutations)
            {
                if (mutation.Content is null)
                    Documents.Remove(Key(mutation.File));
                else
                    Documents[Key(mutation.File)] = mutation.Content;
            }
            return Task.FromResult(new ManagedReceipt(operationId, string.Empty));
        }

        public Task<IReadOnlyList<ManagedDocument>> ScanLearningRecordsAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<ManagedDocument>>([]);

        public Task<IReadOnlyList<ManagedDocument>> ScanLearningDirectoryAsync(
            IReadOnlyList<string> directoryParts,
            int maxResults,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<ManagedDocument>>([]);

        private static string Key(ManagedFile file) =>
            $"{file.Area}/{file.SessionId}/{string.Join('/', file.Parts)}";
    }
}
