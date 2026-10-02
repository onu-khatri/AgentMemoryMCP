using AgentSession.MCP.Extensions;
using AgentSession.MCP.Services;
using AgentSession.MCP.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OnuObservability.Hosting;
using OnuObservability.Hosting.Configuration;
using OnuObservability.Mcp;

var builder = Host.CreateApplicationBuilder(args);
var standardInput = new EofSignalingReadStream(Console.OpenStandardInput());
var standardOutput = Console.OpenStandardOutput();

var observabilityConfiguration = OnuObservabilityCompatibility.Create(builder.Configuration);
var observability = builder.Services
    .AddOnuObservability(observabilityConfiguration)
    .AddAgentMemoryTelemetry();
builder.Services.TryAddSingleton<
    IMcpTelemetryOutcomeClassifier,
    AgentSession.MCP.Observability.AgentMemoryMcpTelemetryOutcomeClassifier>();

var mcp = builder.Services
    .AddMcpServer()
    .WithStreamServerTransport(standardInput, standardOutput);
mcp.WithOnuObservability(observability);
builder.Services
    .AddMemoryStorageConfiguration(builder.Configuration)
    .AddHostedService<MemoryMaintenanceService>();
mcp.WithTools<SharedSessionTools>(MemoryJson.Options)
    .WithTools<MemoryTools>(MemoryJson.Options);

var host = builder.Build();
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
standardInput.OnEndOfStream = lifetime.StopApplication;
await host.StartAsync();
var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
using var registration = lifetime.ApplicationStopping.Register(stopping.SetResult);
await stopping.Task;

var shutdownTimeout = host.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<OnuObservabilityOptions>>()
    .Value.ShutdownFlushTimeoutMilliseconds;
var shutdownStarted = Environment.TickCount64;
using var shutdown = new CancellationTokenSource(TimeSpan.FromMilliseconds(shutdownTimeout));
try
{
    await host.StopAsync(shutdown.Token);
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
catch (Exception) { }

var remaining = Math.Max(
    0,
    shutdownTimeout - (int)(Environment.TickCount64 - shutdownStarted)
);
if (remaining > 0)
{
    var dispose = Task.Run(async () =>
    {
        if (host is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            host.Dispose();
    });
    try
    {
        await dispose.WaitAsync(TimeSpan.FromMilliseconds(remaining));
    }
    catch (TimeoutException) { }
    catch (Exception) { }
}
