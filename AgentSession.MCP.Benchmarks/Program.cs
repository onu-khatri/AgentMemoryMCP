using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using AgentSession.MCP.Extensions;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Observability;
using AgentSession.MCP.Services;
using AgentSession.MCP.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnuObservability.Hosting;
using OnuObservability.Mcp;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var iterations = args.Length > 0 && int.TryParse(args[0], out var parsed)
    ? Math.Clamp(parsed, 25, 10_000)
    : 250;
var output = args.Length > 1 ? Path.GetFullPath(args[1]) : null;
var originalError = Console.Error;
var results = new List<BenchmarkResult>
{
    await MeasureAsync("instrumentation_disabled", iterations, null),
    await MeasureAsync("local_json", iterations, new Scenario(false, null)),
};
await using (var backend = new CountingOtlpBackend())
    results.Add(await MeasureAsync(
        "otlp_batch",
        iterations,
        new Scenario(true, backend)
    ));

var baseline = results[0];
foreach (var result in results)
{
    result.P95OverheadMilliseconds = Math.Max(0, result.P95Milliseconds - baseline.P95Milliseconds);
    result.BudgetPassed = result.P95OverheadMilliseconds <= 5
        && result.CpuMillisecondsPerRequest <= 5
        && result.AllocatedBytesPerRequest <= 131_072
        && result.QueueMemoryBytes <= 67_108_864
        && result.TotalBytesPerRequest <= 32_768;
}
var report = new
{
    generatedAtUtc = DateTimeOffset.UtcNow,
    iterations,
    runtime = Environment.Version.ToString(),
    provisionalBudget = new
    {
        p95OverheadMilliseconds = 5,
        cpuMillisecondsPerRequest = 5,
        allocatedBytesPerRequest = 131_072,
        queueMemoryBytes = 67_108_864,
        totalBytesPerRequest = 32_768,
        productionApprovalRequired = true,
    },
    scenarios = results,
};
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
Console.SetError(originalError);
Console.WriteLine(json);
if (output is not null)
    File.WriteAllText(output, json + Environment.NewLine);
return results.All(result => result.BudgetPassed) ? 0 : 1;

static async Task<BenchmarkResult> MeasureAsync(
    string name,
    int iterations,
    Scenario? scenario
)
{
    var timings = new double[iterations];
    CountingTextWriter? diagnostics = null;
    ServiceProvider? services = null;
    McpDependencyTelemetry? telemetry = null;
    if (scenario is not null)
    {
        diagnostics = new CountingTextWriter();
        Console.SetError(diagnostics);
        var values = new Dictionary<string, string?>
        {
            ["Observability:ExporterEnabled"] = scenario.Export ? "true" : "false",
            ["Observability:ExporterProtocol"] = "http/protobuf",
            ["Observability:Endpoint"] = scenario.Backend is null
                ? null
                : $"http://127.0.0.1:{scenario.Backend.Port}",
            ["Observability:ExportTraces"] = "true",
            ["Observability:ExportMetrics"] = "true",
            ["Observability:ExportLogs"] = scenario.Export ? "true" : "false",
            ["Observability:BatchDelayMilliseconds"] = "60000",
            ["Observability:ExportTimeoutMilliseconds"] = "1000",
            ["Observability:QueueCapacity"] = "2048",
            ["Observability:MaxExportBatchSize"] = "512",
            ["Observability:LogRateLimitBurst"] = "10000",
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var collection = new ServiceCollection();
        collection.AddLogging();
        var observability = collection
            .AddOnuObservability(OnuObservabilityCompatibility.Create(configuration))
            .AddAgentMemoryTelemetry();
        collection.AddSingleton<McpDependencyTelemetry>();
        collection.AddMcpServer().WithOnuObservability(observability);
        services = collection.BuildServiceProvider();
        _ = services.GetRequiredService<TracerProvider>();
        _ = services.GetRequiredService<MeterProvider>();
        telemetry = services.GetRequiredService<McpDependencyTelemetry>();
    }

    async ValueTask InvokeAsync()
    {
        if (telemetry is null)
        {
            await ValueTask.CompletedTask;
            return;
        }
        await telemetry.TrackInternalAsync(
            "maintenance",
            "reindex",
            1,
            _ => Task.CompletedTask,
            CancellationToken.None
        );
    }

    for (var index = 0; index < 25; index++)
        await InvokeAsync();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var process = Process.GetCurrentProcess();
    process.Refresh();
    var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
    var heapBefore = GC.GetTotalMemory(forceFullCollection: false);
    var workingSetBefore = process.WorkingSet64;
    var cpuBefore = process.TotalProcessorTime;
    var total = Stopwatch.StartNew();
    for (var index = 0; index < iterations; index++)
    {
        var started = Stopwatch.GetTimestamp();
        await InvokeAsync();
        timings[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }
    total.Stop();
    var queueMemory = Math.Max(0, GC.GetTotalMemory(forceFullCollection: false) - heapBefore);
    if (services is not null)
    {
        services.GetRequiredService<TracerProvider>().ForceFlush(5_000);
        services.GetRequiredService<MeterProvider>().ForceFlush(5_000);
        await Task.Delay(100);
    }
    process.Refresh();
    var cpu = process.TotalProcessorTime - cpuBefore;
    var workingSetDelta = Math.Max(0, process.WorkingSet64 - workingSetBefore);
    var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
    if (services is not null)
        await services.DisposeAsync();
    Array.Sort(timings);
    var stderrBytes = diagnostics?.BytesWritten ?? 0;
    var otlpBytes = scenario?.Backend?.BodyBytes ?? 0;
    return new BenchmarkResult
    {
        Name = name,
        P50Milliseconds = Percentile(timings, 0.50),
        P95Milliseconds = Percentile(timings, 0.95),
        P99Milliseconds = Percentile(timings, 0.99),
        ThroughputPerSecond = iterations / total.Elapsed.TotalSeconds,
        CpuMillisecondsPerRequest = cpu.TotalMilliseconds / iterations,
        AllocatedBytesPerRequest = allocated / (double)iterations,
        QueueMemoryBytes = queueMemory,
        WorkingSetDeltaBytes = workingSetDelta,
        StderrBytesPerRequest = stderrBytes / (double)iterations,
        OtlpBodyBytesPerRequest = otlpBytes / (double)iterations,
        TotalBytesPerRequest = (stderrBytes + otlpBytes) / (double)iterations,
    };
}

static double Percentile(double[] values, double percentile) =>
    values[Math.Clamp((int)Math.Ceiling(values.Length * percentile) - 1, 0, values.Length - 1)];

internal sealed record Scenario(bool Export, CountingOtlpBackend? Backend);

internal sealed class BenchmarkResult
{
    public required string Name { get; init; }
    public double P50Milliseconds { get; init; }
    public double P95Milliseconds { get; init; }
    public double P99Milliseconds { get; init; }
    public double ThroughputPerSecond { get; init; }
    public double CpuMillisecondsPerRequest { get; init; }
    public double AllocatedBytesPerRequest { get; init; }
    public long QueueMemoryBytes { get; init; }
    public long WorkingSetDeltaBytes { get; init; }
    public double StderrBytesPerRequest { get; init; }
    public double OtlpBodyBytesPerRequest { get; init; }
    public double TotalBytesPerRequest { get; init; }
    public double P95OverheadMilliseconds { get; set; }
    public bool BudgetPassed { get; set; }
}

internal sealed class CountingTextWriter : TextWriter
{
    private long _bytes;
    public override Encoding Encoding => Encoding.UTF8;
    public long BytesWritten => Interlocked.Read(ref _bytes);
    public override void Write(char value) => Interlocked.Add(ref _bytes, Encoding.GetByteCount([value]));
    public override void Write(string? value)
    {
        if (value is not null)
            Interlocked.Add(ref _bytes, Encoding.GetByteCount(value));
    }
    public override void WriteLine(string? value)
    {
        Write(value);
        Interlocked.Add(ref _bytes, Encoding.GetByteCount(Environment.NewLine));
    }
}

internal sealed class CountingOtlpBackend : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _server;
    private long _bodyBytes;
    public CountingOtlpBackend()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _server = ServeAsync();
    }
    public int Port { get; }
    public long BodyBytes => Interlocked.Read(ref _bodyBytes);
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try { await _server; } catch (OperationCanceledException) { }
        _stop.Dispose();
    }
    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_stop.Token); }
            catch (SocketException) when (_stop.IsCancellationRequested) { break; }
            _ = HandleAsync(client);
        }
    }
    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            try
            {
                var header = await ReadHeaderAsync(stream, _stop.Token);
                var match = System.Text.RegularExpressions.Regex.Match(
                    header,
                    "(?im)^Content-Length:\\s*([0-9]+)"
                );
                var remaining = match.Success ? int.Parse(match.Groups[1].Value) : 0;
                Interlocked.Add(ref _bodyBytes, remaining);
                var buffer = new byte[Math.Min(8192, Math.Max(remaining, 1))];
                while (remaining > 0)
                {
                    var read = await stream.ReadAsync(
                        buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                        _stop.Token
                    );
                    if (read == 0) break;
                    remaining -= read;
                }
                var response = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Type: application/x-protobuf\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                );
                await stream.WriteAsync(response, _stop.Token);
            }
            catch (Exception) when (_stop.IsCancellationRequested || !client.Connected) { }
            catch (IOException) { }
        }
    }
    private static async Task<string> ReadHeaderAsync(NetworkStream stream, CancellationToken token)
    {
        var bytes = new List<byte>(1024);
        var tail = 0;
        while (bytes.Count < 64 * 1024 && tail != 0x0D0A0D0A)
        {
            var buffer = new byte[1];
            if (await stream.ReadAsync(buffer, token) == 0) break;
            bytes.Add(buffer[0]);
            tail = (tail << 8) | buffer[0];
        }
        return Encoding.ASCII.GetString(bytes.ToArray());
    }
}
