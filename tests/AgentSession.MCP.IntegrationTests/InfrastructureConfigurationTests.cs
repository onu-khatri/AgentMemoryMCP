using AgentSession.MCP.Infrastructure.DependencyInjection;
using AgentSession.MCP.Infrastructure.Configuration;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.IntegrationTests;

public sealed class InfrastructureConfigurationTests
{
    [Fact]
    public void DefaultRootUsesUserProfileAndExplicitRepository()
    {
        using var services = Build(
            new Dictionary<string, string?> { ["Repository:Id"] = "example-repo" }
        );
        var paths = services.GetRequiredService<ManagedStoragePaths>();
        Assert.Equal(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex",
                "AgentMemory"
            ),
            paths.Root
        );
        Assert.Equal(
            Path.Combine(paths.Root, "sessions", "example-repo"),
            paths.SessionsRoot
        );
        Assert.Equal(
            Path.Combine(paths.Root, "repositories", "example-repo", "AiLearning"),
            paths.LearningRoot
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("../other")]
    [InlineData("other/repo")]
    [InlineData("OtherRepo")]
    [InlineData("con")]
    [InlineData("repo:stream")]
    public void InvalidIdentityIsRejectedInsteadOfSanitized(string identity)
    {
        using var services = Build(
            new Dictionary<string, string?> { ["Repository:Id"] = identity }
        );
        Assert.Throws<OptionsValidationException>(() =>
            services.GetRequiredService<ManagedStoragePaths>()
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/directory")]
    [InlineData("\\\\server\\share")]
    [InlineData("//server/share")]
    public void InvalidStorageRootIsRejected(string root)
    {
        using var services = Build(
            new Dictionary<string, string?>
            {
                ["Repository:Id"] = "repo",
                ["SystemStorage:Root"] = root,
            }
        );
        Assert.Throws<OptionsValidationException>(() =>
            services.GetRequiredService<ManagedStoragePaths>()
        );
    }

    [Theory]
    [InlineData("Memory:Limits:MaxRecordBytes", "0")]
    [InlineData("Memory:Limits:MaxMutationBatch", "0")]
    [InlineData("Memory:Limits:MaxRecallResults", "0")]
    [InlineData("Memory:Limits:MaxArchiveCompressedBytes", "1")]
    public void InvalidStorageLimitsAreRejected(string key, string value)
    {
        using var services = Build(
            new Dictionary<string, string?>
            {
                ["Repository:Id"] = "repo",
                [key] = value,
            }
        );
        Assert.Throws<OptionsValidationException>(() =>
            services.GetRequiredService<IOptions<ManagedStorageOptions>>().Value
        );
    }

    [Fact]
    public void EnvironmentOverridesConfigurationWithoutCreatingDirectories()
    {
        var prefix = $"MEMORY_TEST_{Guid.NewGuid():N}_";
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable(prefix + "SystemStorage__Root", root);
            Environment.SetEnvironmentVariable(prefix + "Repository__Id", "env-repo");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Repository:Id"] = "base-repo" }
                )
                .AddEnvironmentVariables(prefix)
                .Build();
            using var services = new ServiceCollection()
                .AddAgentSessionInfrastructure(configuration)
                .BuildServiceProvider();
            var paths = services.GetRequiredService<ManagedStoragePaths>();
            Assert.Equal(root, paths.Root);
            Assert.Equal("env-repo", paths.RepositoryId);
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            Environment.SetEnvironmentVariable(prefix + "SystemStorage__Root", null);
            Environment.SetEnvironmentVariable(prefix + "Repository__Id", null);
        }
    }

    [Theory]
    [InlineData("Repository:Id", "")]
    [InlineData("Repository:Id", "private-repository-canary/escape")]
    [InlineData("SystemStorage:Root", "private-root-canary")]
    [InlineData("Memory:Limits:MaxMutationBatch", "0")]
    public async Task InvalidStorageConfigurationFailsAtStartupWithoutEchoingValues(
        string key,
        string value
    )
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Repository:Id"] = "repo",
                [key] = value,
            }
        );
        builder.Services.AddAgentSessionInfrastructure(builder.Configuration);
        using var host = builder.Build();

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        if (!string.IsNullOrEmpty(value))
            Assert.DoesNotContain(value, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceProvider Build(Dictionary<string, string?> values) =>
        new ServiceCollection()
            .AddAgentSessionInfrastructure(
                new ConfigurationBuilder().AddInMemoryCollection(values).Build()
            )
            .BuildServiceProvider();
}
