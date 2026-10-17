using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Infrastructure.DependencyInjection;
using AgentSession.MCP.Services;
using AgentSession.MCP.Session.Contracts.Capabilities;
using AgentSession.MCP.Session.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnuObservability.Mcp.Health;

if (args.Length != 2) return 2;
using var services = new ServiceCollection()
    .AddSingleton<IMcpDependencyFailureRecorder, NoOpDependencyFailureRecorder>()
    .AddAgentSessionInfrastructure(Configuration())
    .AddAgentSessionModule(Configuration())
    .BuildServiceProvider();
if (args[0] == "lock")
{
    await using var lease = await services.GetRequiredService<RepositoryMutationLock>().AcquireAsync(TimeSpan.FromSeconds(10), default);
    Console.WriteLine("locked");
    await Console.In.ReadLineAsync();
    return 0;
}
if (args[0] == "coordinate")
{
    Console.WriteLine("ready");
    var json = await Console.In.ReadLineAsync();
    try
    {
        var request = JsonSerializer.Deserialize<CoordinateTaskRequest>(json!, SessionJson.Options)!;
        var context = new PlatformRequestContext(
            new ClientIdentity("test-worker"),
            null,
            null,
            new HashSet<string>(StringComparer.Ordinal),
            new RepositoryResource("repo"),
            "test-worker-coordinate"
        );
        var result = await services
            .GetRequiredService<ISessionCoordinationCapabilities>()
            .CoordinateAsync(request, context, default);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions(SessionJson.Options) { WriteIndented = false }));
        return 0;
    }
    catch (ValidationException)
    {
        Console.WriteLine("conflict");
        return 3;
    }
}
return 2;

IConfiguration Configuration() => new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    { ["SystemStorage:Root"] = args[1], ["Repository:Id"] = "repo" })
    .Build();

internal sealed class NoOpDependencyFailureRecorder : IMcpDependencyFailureRecorder
{
    public void Record(string dependencyType, string dependencyOperation)
    {
    }
}
