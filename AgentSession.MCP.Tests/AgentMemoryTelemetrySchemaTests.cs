using AgentSession.MCP.Extensions;
using AgentSession.MCP.Core.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnuObservability.Hosting.Composition;
using OnuObservability.Mcp;
using OnuObservability.Schema;

namespace AgentSession.MCP.Tests;

public sealed class AgentMemoryTelemetrySchemaTests
{
    [Fact]
    public void ApplicationDescriptorsAreRegisteredOnce()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OnuObservability:Enabled"] = "false",
                ["OnuObservability:ServiceName"] = "agent-memory-test",
            })
            .Build();
        var observability = services
            .AddOnuObservability(configuration)
            .AddAgentMemoryTelemetry();
        services.AddMcpServer().WithOnuObservability(observability);
        using var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredService<TelemetrySchemaSnapshot>();
        var applicationOperations = schema.Operations
            .Where(operation => operation.Name.StartsWith("agentmemory.", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(17, applicationOperations.Length);
        Assert.Equal(17, applicationOperations.Select(operation => operation.Name).Distinct().Count());

    }

    [Fact]
    public void RuntimeCompositionDoesNotRegisterTheLegacyDuplicateMcpMeter()
    {
        var services = new ServiceCollection();
        services.AddMemoryStorageConfiguration(new ConfigurationBuilder().Build());

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.FullName == "AgentSession.MCP.Observability.McpTelemetry");
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDependencyOperationTracker));
    }
}
