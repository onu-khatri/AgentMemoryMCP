using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Core.Errors;
using AgentSession.MCP.Core.Execution;
using AgentSession.MCP.Core.Observability;
using AgentSession.MCP.Core.Policies;
using AgentSession.MCP.Core.Security;
using AgentSession.MCP.Core.Storage;
using AgentSession.MCP.Helpers;

namespace AgentSession.MCP.UnitTests;

public sealed class CoreContractTests
{
    [Fact]
    public void Request_context_preserves_optional_identity_and_normalizes_duplicate_scopes()
    {
        var context = Context(new HashSet<string> { "memory.read", "memory.read" });
        Assert.Null(context.TenantId);
        Assert.Null(context.UserId);
        Assert.Single(context.Scopes);
        Assert.Equal("correlation", context.CorrelationId);
    }

    [Fact]
    public void Correlation_ids_are_bounded_and_reject_untrusted_delimiters()
    {
        Assert.True(PlatformRequestContext.IsValidCorrelationId("trace-123/abc"));
        Assert.False(PlatformRequestContext.IsValidCorrelationId(new string('a', PlatformRequestContext.MaxCorrelationIdLength + 1)));
        Assert.False(PlatformRequestContext.IsValidCorrelationId("trace\r\nheader"));
        Assert.Throws<ArgumentException>(() => new PlatformRequestContext(
            new ClientIdentity("client"), null, null, new HashSet<string>(), new RepositoryResource("repo"), "trace\r\nheader"));
    }

    [Theory]
    [InlineData("client")]
    [InlineData("repository")]
    [InlineData("correlation")]
    public void Blank_required_context_identity_is_rejected(string member)
    {
        Assert.Throws<ArgumentException>(() => member switch
        {
            "client" => new PlatformRequestContext(new ClientIdentity(" "), null, null, new HashSet<string>(), new RepositoryResource("repo"), "correlation"),
            "repository" => new PlatformRequestContext(new ClientIdentity("client"), null, null, new HashSet<string>(), new RepositoryResource(" "), "correlation"),
            _ => new PlatformRequestContext(new ClientIdentity("client"), null, null, new HashSet<string>(), new RepositoryResource("repo"), " "),
        });
    }

    [Fact]
    public void Error_categories_are_exact_and_platform_error_guards_text()
    {
        Assert.Equal(
        ["ValidationError", "Unauthorized", "Forbidden", "NotFound", "Conflict", "RateLimited", "DependencyUnavailable", "Timeout", "Cancelled", "Unexpected"],
            Enum.GetNames<ErrorCategory>());
        Assert.False(new PlatformError(ErrorCategory.ValidationError, "invalid", "message").IsTransient);
        Assert.True(new PlatformError(ErrorCategory.Timeout, "timeout", "message", true).IsTransient);
        Assert.Throws<ArgumentException>(() => new PlatformError(ErrorCategory.Unexpected, " ", "message"));
        Assert.Throws<ArgumentException>(() => new PlatformError(ErrorCategory.Unexpected, "code", " "));
    }

    [Fact]
    public void Result_enforces_exactly_one_branch()
    {
        var success = Result<string>.Success("value");
        Assert.True(success.IsSuccess);
        Assert.Equal("value", success.Value);
        Assert.Throws<InvalidOperationException>(() => success.Error);
        var failure = Result<string>.Failure(new(ErrorCategory.NotFound, "missing", "not found"));
        Assert.False(failure.IsSuccess);
        Assert.Equal("missing", failure.Error.Code);
        Assert.Throws<InvalidOperationException>(() => failure.Value);
        Assert.Throws<ArgumentNullException>(() => Result<string>.Success(null!));
        Assert.Throws<ArgumentNullException>(() => Result<string>.Failure(null!));
    }

    [Fact]
    public void Validation_exception_preserves_exact_identity_and_codes()
    {
        Assert.Equal("AgentSession.MCP.Helpers.ValidationException", typeof(ValidationException).FullName);
        Assert.Equal("invalid_request", new ValidationException("message").Code);
        Assert.Equal("custom", new ValidationException("message", "custom").Code);
    }

    [Fact]
    public void Bounds_are_inclusive_and_invalid_policies_are_rejected()
    {
        var bound = new BoundedInt(1, 3);
        Assert.False(bound.Contains(0));
        Assert.True(bound.Contains(1));
        Assert.True(bound.Contains(2));
        Assert.True(bound.Contains(3));
        Assert.False(bound.Contains(4));
        Assert.Throws<ArgumentException>(() => new BoundedInt(3, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedInt(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionBounds(bound, bound, bound, bound, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(RetryEligibility.SafeRead, IdempotencyRequirement.NotApplicable, true, false, null, true)]
    [InlineData(RetryEligibility.IdempotentWrite, IdempotencyRequirement.Required, true, false, "key", true)]
    [InlineData(RetryEligibility.IdempotentWrite, IdempotencyRequirement.Required, true, false, " ", false)]
    [InlineData(RetryEligibility.IdempotentWrite, IdempotencyRequirement.Supported, true, false, null, true)]
    [InlineData(RetryEligibility.Never, IdempotencyRequirement.NotApplicable, true, false, null, false)]
    [InlineData(RetryEligibility.SafeRead, IdempotencyRequirement.NotApplicable, false, false, null, false)]
    [InlineData(RetryEligibility.SafeRead, IdempotencyRequirement.NotApplicable, true, true, null, false)]
    public void Retry_policy_is_eligibility_only(
        RetryEligibility eligibility,
        IdempotencyRequirement idempotency,
        bool transient,
        bool cancelled,
        string? key,
        bool expected)
    {
        Assert.Equal(expected, new RetryAndIdempotencyPolicy(eligibility, idempotency).CanRetry(transient, cancelled, key));
    }

    [Fact]
    public void Descriptor_rejects_duplicate_operations()
    {
        var auth = new AuthorizationRequirement("operation", true, new HashSet<string>(), ResourceAccessRequirement.RepositoryBound);
        var capability = new CapabilityDescriptor("operation", CapabilityAccess.Read, auth);
        Assert.Throws<ArgumentException>(() => new ModuleRegistrationDescriptor(
            "module", [typeof(IDisposable)], [capability, capability], [], new HashSet<ErrorCategory>(), [], []));
    }

    [Theory]
    [InlineData("repo", true)]
    [InlineData("repo-1", true)]
    [InlineData("con", false)]
    [InlineData("Repo", false)]
    [InlineData("-repo", false)]
    [InlineData("repo-", false)]
    [InlineData("repo--one", false)]
    [InlineData("repo/one", false)]
    public void Managed_storage_identifier_preserves_the_portable_rule(
        string value,
        bool expected
    )
    {
        Assert.Equal(expected, ManagedStorageIdentifier.IsValid(value));
        if (expected)
            ManagedStorageIdentifier.RequireValid(value);
        else
            Assert.Throws<ValidationException>(() => ManagedStorageIdentifier.RequireValid(value));
    }

    [Fact]
    public void Content_hash_is_stable_lowercase_sha256()
    {
        const string expected =
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        Assert.Equal(expected, ContentHash.Compute(string.Empty));
        Assert.Equal(expected, ContentHash.Compute([]));
    }

    [Fact]
    public void Managed_storage_contract_shapes_preserve_existing_members()
    {
        var file = new ManagedFile(ManagedArea.Session, "session", ["record.json"]);
        var mutation = new ManagedMutation(file, "content", "hash");
        var commit = new ManagedCommit("operation", "request", [mutation]);

        Assert.Equal("session", file.SessionId);
        Assert.Same(mutation, commit.Mutations[0]);
        Assert.Equal(
            [
                "ScanLearningDirectoryAsync",
                "ScanLearningRecordsAsync",
                "GetRecoveryBacklogAsync",
                "ReadAsync",
                "ReadManyAsync",
                "ListSessionIdsAsync",
                "CommitAsync",
            ],
            typeof(IManagedTransactionStore)
                .GetMethods()
                .Select(method => method.Name)
                .Order(StringComparer.Ordinal)
                .OrderBy(name => Array.IndexOf(
                    [
                        "ScanLearningDirectoryAsync",
                        "ScanLearningRecordsAsync",
                        "GetRecoveryBacklogAsync",
                        "ReadAsync",
                        "ReadManyAsync",
                        "ListSessionIdsAsync",
                        "CommitAsync",
                    ],
                    name
                ))
        );
    }

    [Fact]
    public void Dependency_operation_rejects_blank_names_and_carries_only_bounded_metadata()
    {
        var operation = DependencyOperation.External("ollama", "embedding", 3);
        Assert.Equal(DependencyOperationKind.External, operation.Kind);
        Assert.Equal(3, operation.ItemCount);
        Assert.Throws<ArgumentException>(() => DependencyOperation.Internal(" ", "lock"));
        Assert.DoesNotContain(
            typeof(DependencyOperationMetadata).GetProperties(),
            property => property.PropertyType == typeof(string)
        );
    }

    private static PlatformRequestContext Context(IReadOnlySet<string>? scopes = null) => new(
        new("client"), null, null, scopes ?? new HashSet<string>(), new("repository"), "correlation");
}
