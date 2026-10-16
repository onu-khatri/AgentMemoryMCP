using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Infrastructure.DependencyInjection;
using AgentSession.MCP.Options;
using AgentSession.MCP.Services;
using AgentSession.MCP.Session.Contracts.Capabilities;
using AgentSession.MCP.Session.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.IntegrationTests;

public sealed class SessionRegistrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "session-registration-tests",
        Guid.NewGuid().ToString("N")
    );

    [Fact]
    public void Infrastructure_and_session_resolve_singleton_capability_aliases_and_repository()
    {
        var configuration = Configuration();
        using var provider = new ServiceCollection()
            .AddAgentSessionInfrastructure(configuration)
            .AddAgentSessionModule(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.Same(
            provider.GetRequiredService<SharedSessionLifecycleService>(),
            provider.GetRequiredService<ISessionLifecycleCapabilities>()
        );
        Assert.Same(
            provider.GetRequiredService<SessionCoordinationService>(),
            provider.GetRequiredService<ISessionCoordinationCapabilities>()
        );
        Assert.Same(
            provider.GetRequiredService<SessionResumeService>(),
            provider.GetRequiredService<ISessionResumeCapabilities>()
        );
        Assert.Equal("repo", provider.GetRequiredService<RepositoryResource>().RepositoryId);
    }

    [Fact]
    public void Session_registration_does_not_hide_missing_infrastructure_dependencies()
    {
        using var provider = new ServiceCollection()
            .AddAgentSessionModule(Configuration())
            .BuildServiceProvider();

        var error = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<ISessionCoordinationCapabilities>()
        );
        Assert.Contains("IManagedTransactionStore", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SessionCoordination:ClaimLeaseMinutes", "0", typeof(SessionCoordinationOptions))]
    [InlineData("SessionCoordination:SnapshotLifetimeMinutes", "NaN", typeof(SessionCoordinationOptions))]
    [InlineData("Memory:Limits:MaxContentBytes", "0", typeof(SessionLimitsOptions))]
    [InlineData("Memory:Limits:MaxRecordBytes", "1", typeof(SessionLimitsOptions))]
    public void Invalid_session_configuration_fails_validation(
        string key,
        string value,
        Type optionsType
    )
    {
        var values = Values();
        values[key] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using var provider = new ServiceCollection()
            .AddAgentSessionModule(configuration)
            .BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => optionsType == typeof(SessionCoordinationOptions)
            ? provider.GetRequiredService<IOptions<SessionCoordinationOptions>>().Value
            : provider.GetRequiredService<IOptions<SessionLimitsOptions>>().Value);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(Values()).Build();

    private Dictionary<string, string?> Values() => new()
    {
        ["SystemStorage:Root"] = _root,
        ["Repository:Id"] = "repo",
    };
}
