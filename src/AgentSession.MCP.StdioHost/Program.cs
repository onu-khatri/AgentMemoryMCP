using AgentSession.MCP.AgentMemory.DependencyInjection;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Infrastructure.Configuration;
using AgentSession.MCP.Infrastructure.DependencyInjection;
using AgentSession.MCP.Protocol.Registration;
using AgentSession.MCP.ServiceDefaults;
using AgentSession.MCP.Session.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnuObservability.Hosting.Composition;
using OnuObservability.Mcp;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<HostOptions>(options =>
{
    var seconds = builder.Configuration.GetValue<int?>("Host:ShutdownTimeoutSeconds") ?? 10;
    options.ShutdownTimeout = TimeSpan.FromSeconds(seconds);
});
builder.Logging.ClearProviders();
builder.AddServiceDefaults("agent-session-mcp-stdio");
var observabilityConfiguration = new ConfigurationBuilder()
    .AddConfiguration(builder.Configuration)
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["OnuObservability:ServiceName"] = "agent-session-mcp",
        ["OnuObservability:ServiceNamespace"] = "agent-memory",
        ["OnuObservability:ExporterEnabled"] = "false",
        ["OnuObservability:LocalJsonLoggingEnabled"] = "true",
        ["OnuObservability:Mcp:StdioSafeLogging"] = "true"
    })
    .Build();
builder.Services.AddAgentSessionInfrastructure(builder.Configuration);
builder.Services.AddAgentSessionModule(builder.Configuration);
builder.Services.AddAgentMemoryModule(builder.Configuration);
// Stdio has one authenticated process identity; the scoped protocol pipeline reads this
// process context without allowing a singleton pipeline to capture a scoped accessor.
builder.Services.TryAddSingleton<IPlatformRequestContextAccessor, PlatformRequestContextAccessor>();

var standardInput = new EofSignalingReadStream(Console.OpenStandardInput());
var standardOutput = Console.OpenStandardOutput();
var mcp = builder.Services
    .AddMcpServer()
    .WithStreamServerTransport(standardInput, standardOutput);
mcp.WithOnuObservability(builder.Services.AddOnuObservability(observabilityConfiguration));
mcp.AddAgentSessionProtocol();

using var host = builder.Build();
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
standardInput.OnEndOfStream = lifetime.StopApplication;
var accessor = host.Services.GetRequiredService<IPlatformRequestContextAccessor>();
var repository = host.Services.GetRequiredService<IOptions<RepositoryOptions>>().Value;
accessor.Set(new PlatformRequestContext(
    new ClientIdentity("stdio"),
    null,
    null,
    new HashSet<string>([
        "mcp:session:read", "mcp:session:write",
        "mcp:agent-memory:read", "mcp:agent-memory:write", "mcp:agent-memory:maintenance"
    ], StringComparer.Ordinal),
    new RepositoryResource(repository.Id),
    "stdio"));

try
{
    await host.RunAsync();
}
finally
{
    accessor.Clear();
    await standardInput.DisposeAsync();
    await standardOutput.DisposeAsync();
}

internal sealed class EofSignalingReadStream(Stream inner) : Stream
{
    private Action? onEndOfStream;
    private int signaled;

    public Action? OnEndOfStream { set => onEndOfStream = value; }
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }
    public override void Flush() => inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => SignalIfEof(inner.Read(buffer, offset, count));
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        SignalIfEof(await inner.ReadAsync(buffer, cancellationToken));
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        SignalIfEof(await inner.ReadAsync(buffer, offset, count, cancellationToken));
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); GC.SuppressFinalize(this); }
    private int SignalIfEof(int bytesRead)
    {
        if (bytesRead == 0 && Interlocked.Exchange(ref signaled, 1) == 0) onEndOfStream?.Invoke();
        return bytesRead;
    }
}
