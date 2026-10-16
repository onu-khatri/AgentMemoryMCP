using AgentSession.MCP.Extensions;
using AgentSession.MCP.Options;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.IntegrationTests;

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
    public async Task MissingIdentityFailsAtHostStartup()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        builder.Services.AddMemoryStorageConfiguration(builder.Configuration);
        using var host = builder.Build();
        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Theory]
    [InlineData("Embedding:TimeoutSeconds", "0")]
    [InlineData("Embedding:Ollama:BaseUrl", "http://user:private-secret@localhost")]
    [InlineData("Qdrant:Host", "private-provider.example")]
    [InlineData("Memory:Limits:MaxMutationBatch", "0")]
    public async Task InvalidModuleAndProviderSettingsFailAtStartupWithoutEchoingValues(
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
        builder.Services.AddMemoryStorageConfiguration(builder.Configuration);
        using var host = builder.Build();

        var error = await Record.ExceptionAsync(() => host.StartAsync());

        Assert.NotNull(error);
        Assert.True(
            error is OptionsValidationException
                || error is AggregateException aggregate
                    && aggregate.InnerExceptions.All(inner => inner is OptionsValidationException),
            $"Expected only options validation failures, but received {error.GetType().Name}."
        );
        Assert.DoesNotContain(value, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceProvider Build(Dictionary<string, string?> values)
        => new ServiceCollection().AddMemoryStorageConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build()).BuildServiceProvider();
}
