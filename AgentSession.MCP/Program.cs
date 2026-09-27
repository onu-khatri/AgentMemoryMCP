using AgentSession.MCP.Extensions;
using AgentSession.MCP.Services;
using AgentSession.MCP.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder
    .Services.AddMemoryStorageConfiguration(builder.Configuration)
    .AddHostedService<MemoryMaintenanceService>()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SharedSessionTools>(MemoryJson.Options)
    .WithTools<MemoryTools>(MemoryJson.Options);

await builder.Build().RunAsync();
