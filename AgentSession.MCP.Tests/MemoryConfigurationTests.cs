using AgentSession.MCP.Extensions;
using AgentSession.MCP.Options;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Tests;

public sealed class MemoryConfigurationTests
{
    [Fact]
    public void PolicyDefaultsMatchLocalDeployment()
    {
        using var services = Build(new Dictionary<string, string?> { ["Repository:Id"] = "repo" });
        var policy = services.GetRequiredService<IOptions<MemoryPolicyOptions>>().Value;
        Assert.Equal(24, policy.Temp.MaxAgeHours);
        Assert.Equal(60, policy.Temp.CleanupIntervalMinutes);
        Assert.Equal(3, policy.Short.ReviewAfterDays);
        Assert.Equal(7, policy.Short.CompactionCandidateAfterDays);
        Assert.Equal(14, policy.Short.ArchiveAfterDays);
        Assert.Equal(2, policy.Long.RequiredIndependentConfirmations);
        Assert.Equal(10, policy.Long.DefaultMaxResults);
        Assert.Same(TimeProvider.System, services.GetRequiredService<TimeProvider>());
        Assert.Equal("embeddinggemma", services.GetRequiredService<IOptions<EmbeddingOptions>>().Value.Ollama.Model);
        Assert.Equal(6334, services.GetRequiredService<IOptions<QdrantOptions>>().Value.GrpcPort);
    }

    [Theory]
    [InlineData("Memory:Temp:MaxAgeHours", "25")]
    [InlineData("Memory:Temp:MaxAgeHours", "NaN")]
    [InlineData("Memory:Temp:CleanupIntervalMinutes", "0")]
    [InlineData("Memory:Short:ArchiveAfterDays", "Infinity")]
    [InlineData("Memory:Limits:MaxMutationBatch", "0")]
    [InlineData("Memory:Limits:MaxContentBytes", "2000000")]
    [InlineData("Memory:Long:DefaultMaxResults", "101")]
    [InlineData("Memory:Long:MinimumSimilarity", "NaN")]
    [InlineData("Memory:Long:MinimumSimilarity", "1.1")]
    public void InvalidPoliciesFail(string key, string value)
    {
        using var services = Build(new Dictionary<string, string?> { [key] = value });
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<MemoryPolicyOptions>>().Value);
    }

    [Theory]
    [InlineData("Embedding:Ollama:BaseUrl", "http://example.com")]
    [InlineData("Embedding:Ollama:BaseUrl", "http://localhost@example.com")]
    [InlineData("Embedding:Ollama:BaseUrl", "http://user:password@localhost")]
    [InlineData("Embedding:Ollama:BaseUrl", "file:///tmp/model")]
    [InlineData("Embedding:TimeoutSeconds", "NaN")]
    [InlineData("Embedding:Provider", "cloud")]
    public void InvalidEmbeddingOptionsFail(string key, string value)
    {
        using var services = Build(new Dictionary<string, string?> { [key] = value });
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<EmbeddingOptions>>().Value);
    }

    [Theory]
    [InlineData("Qdrant:Host", "remote.example")]
    [InlineData("Qdrant:GrpcPort", "65536")]
    [InlineData("Qdrant:RestPort", "0")]
    [InlineData("Qdrant:Collection", "../another")]
    public void InvalidVectorOptionsFail(string key, string value)
    {
        using var services = Build(new Dictionary<string, string?> { [key] = value });
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<QdrantOptions>>().Value);
    }

    [Fact]
    public void DefaultRootUsesUserProfileAndExplicitRepository()
    {
        using var services = Build(new Dictionary<string, string?> { ["Repository:Id"] = "example-repo" });
        var paths = services.GetRequiredService<MemoryStoragePaths>();
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex", "AgentMemory"), paths.Root);
        Assert.Equal(Path.Combine(paths.Root, "sessions", "example-repo"), paths.SessionsRoot);
        Assert.Equal(Path.Combine(paths.Root, "repositories", "example-repo", "AiLearning"), paths.LearningRoot);
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
        using var services = Build(new Dictionary<string, string?> { ["Repository:Id"] = identity });
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<MemoryStoragePaths>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/directory")]
    [InlineData("\\\\server\\share")]
    [InlineData("//server/share")]
    public void InvalidStorageRootIsRejected(string root)
    {
        using var services = Build(new Dictionary<string, string?>
        {
            ["Repository:Id"] = "repo", ["SystemStorage:Root"] = root
        });
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<MemoryStoragePaths>());
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
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Repository:Id"] = "base-repo" })
                .AddEnvironmentVariables(prefix).Build();
            using var services = new ServiceCollection().AddMemoryStorageConfiguration(config).BuildServiceProvider();
            var paths = services.GetRequiredService<MemoryStoragePaths>();
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

    [Fact]
    public async Task MissingIdentityFailsAtHostStartup()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        builder.Services.AddMemoryStorageConfiguration(builder.Configuration);
        using var host = builder.Build();
        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    private static ServiceProvider Build(Dictionary<string, string?> values)
        => new ServiceCollection().AddMemoryStorageConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build()).BuildServiceProvider();
}
