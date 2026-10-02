using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Extensions;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnuObservability.Mcp;

if (args.Length != 2) return 2;
using var services = new ServiceCollection()
    .AddSingleton<IMcpDependencyFailureRecorder, NoOpDependencyFailureRecorder>()
    .AddMemoryStorageConfiguration(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["SystemStorage:Root"] = args[1], ["Repository:Id"] = "repo" }).Build())
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
        var request = JsonSerializer.Deserialize<CoordinateTaskRequest>(json!, MemoryJson.Options)!;
        var result = await services.GetRequiredService<SessionCoordinationService>().CoordinateAsync(request);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions(MemoryJson.Options) { WriteIndented = false }));
        return 0;
    }
    catch (ValidationException)
    {
        Console.WriteLine("conflict");
        return 3;
    }
}
return 2;

internal sealed class NoOpDependencyFailureRecorder : IMcpDependencyFailureRecorder
{
    public void Record(string dependencyType, string dependencyOperation)
    {
    }
}
