using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentSession.MCP.Tests;

[Collection("CollectorIntegration")]
public sealed class CollectorIntegrationTests
{
    private const string FailureProject = "agent-memory-observability-failure";

    [CollectorFact]
    [Trait("Category", "CollectorIntegration")]
    public async Task LocalCollectorReceivesCorrelatedTracesMetricsAndLogsWithoutBreakingMcp()
    {
        var root = Path.Combine(Path.GetTempPath(), "collector-happy", Guid.NewGuid().ToString("N"));
        var capture = Environment.GetEnvironmentVariable("AGENT_MEMORY_COLLECTOR_CAPTURE_FILE")
            ?? throw new InvalidOperationException(
                "AGENT_MEMORY_COLLECTOR_CAPTURE_FILE must identify the test overlay output."
            );
        var marker = "collector-test-" + Guid.NewGuid().ToString("N");
        try
        {
            using var process = new McpProcess(
                root,
                environment: ExportEnvironment("http://127.0.0.1:4318", marker)
            );
            await process.InitializeAsync();
            var result = await process.CallAsync(
                "create_or_activate_session",
                new
                {
                    sessionId = "collector-session",
                    actorId = "collector-agent",
                    operationId = "collector-activate",
                }
            );
            Assert.Equal("collector-session", result.GetProperty("sessionId").GetString());
            var stderr = await process.StopAndReadStderrAsync();
            Assert.False(process.ForcedTermination);
            var exporterDiagnostics = stderr.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries
            ).Where(line => line.Contains("mcp.telemetry.dropped", StringComparison.Ordinal)).ToArray();
            Assert.True(exporterDiagnostics.Length == 0, string.Join(Environment.NewLine, exporterDiagnostics));
            Assert.All(
                stderr.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries),
                line => JsonDocument.Parse(line).Dispose()
            );

            // The scratch collector image keeps the file exporter open exclusively on
            // Windows bind mounts. A graceful stop flushes and unlocks the test capture.
            await RunDockerAsync("stop", "agent-memory-observability-collector-1");
            await WaitUntilAsync(async () =>
            {
                return File.Exists(capture)
                    && (await File.ReadAllTextAsync(capture)).Contains(marker, StringComparison.Ordinal);
            }, TimeSpan.FromSeconds(15));

            var exported = await File.ReadAllTextAsync(capture);
            Assert.Contains("resourceSpans", exported, StringComparison.Ordinal);
            Assert.Contains("resourceMetrics", exported, StringComparison.Ordinal);
            Assert.Contains("resourceLogs", exported, StringComparison.Ordinal);
            Assert.Contains("agent-session-mcp", exported, StringComparison.Ordinal);
            Assert.Contains(marker, exported, StringComparison.Ordinal);
            Assert.Contains("mcp.tool.invocations", exported, StringComparison.Ordinal);
            Assert.Contains("mcp.tool.completed", exported, StringComparison.Ordinal);
            var traceIds = Regex.Matches(exported, "\\\"traceId\\\":\\\"([^\\\"]+)\\\"")
                .Select(match => match.Groups[1].Value)
                .Where(value => value.Length > 0)
                .ToArray();
            Assert.Contains(traceIds.GroupBy(value => value), group => group.Count() >= 2);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [CollectorFailureFact]
    [Trait("Category", "CollectorIntegration")]
    public async Task SlowFullBackendDoesNotBlockMcpAndQueuedExportRecovers()
    {
        await using var backend = new FailureBackend();
        var root = Path.Combine(
            Path.GetTempPath(),
            "collector-failure",
            Guid.NewGuid().ToString("N")
        );
        try
        {
            backend.Mode = FailureBackendMode.SlowFailure;
            await RunFailureComposeAsync("up", backend.Port);
            await WaitUntilAsync(
                async () => (await GetStringAsync("http://127.0.0.1:14133/")) is not null,
                TimeSpan.FromSeconds(30)
            );

            using var process = new McpProcess(
                root,
                environment: ExportEnvironment(
                    "http://127.0.0.1:14318",
                    "collector-failure-test"
                )
            );
            await process.InitializeAsync();
            var longestCall = TimeSpan.Zero;
            for (var index = 0; index < 40; index++)
            {
                var started = Stopwatch.GetTimestamp();
                var result = await process.CallAsync(
                    "create_or_activate_session",
                    new
                    {
                        sessionId = "failure-session",
                        actorId = "failure-agent",
                        operationId = "failure-operation-" + index,
                    }
                );
                Assert.Equal("failure-session", result.GetProperty("sessionId").GetString());
                longestCall = Max(longestCall, Stopwatch.GetElapsedTime(started));
            }
            var shutdownStarted = Stopwatch.GetTimestamp();
            var stderr = await process.StopAndReadStderrAsync();
            var shutdownDuration = Stopwatch.GetElapsedTime(shutdownStarted);

            Assert.False(process.ForcedTermination);
            Assert.True(longestCall < TimeSpan.FromSeconds(2), longestCall.ToString());
            Assert.True(shutdownDuration < TimeSpan.FromSeconds(4), shutdownDuration.ToString());
            Assert.All(
                stderr.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries),
                line => JsonDocument.Parse(line).Dispose()
            );

            string failureMetrics = string.Empty;
            try
            {
                await WaitUntilAsync(async () =>
                {
                    failureMetrics = await GetStringAsync("http://127.0.0.1:18888/metrics")
                        ?? string.Empty;
                    return failureMetrics.Contains("otelcol_exporter_send_failed", StringComparison.Ordinal)
                        && failureMetrics.Contains("otelcol_exporter_enqueue_failed", StringComparison.Ordinal);
                }, TimeSpan.FromSeconds(8));
            }
            catch (TimeoutException error)
            {
                throw new Xunit.Sdk.XunitException(
                    error.Message + Environment.NewLine + failureMetrics
                );
            }
            Assert.Contains("otelcol_exporter_queue_capacity", failureMetrics, StringComparison.Ordinal);
            Assert.Contains("} 4", failureMetrics, StringComparison.Ordinal);
            Assert.Contains("otelcol_receiver_accepted", failureMetrics, StringComparison.Ordinal);

            backend.Mode = FailureBackendMode.Healthy;
            await WaitUntilAsync(async () =>
            {
                var metrics = await GetStringAsync("http://127.0.0.1:18888/metrics")
                    ?? string.Empty;
                return backend.SuccessfulRequests > 0
                    && QueueSizes(metrics).All(size => size == 0);
            }, TimeSpan.FromSeconds(8));
            Assert.True(backend.SuccessfulRequests > 0);
        }
        finally
        {
            await RunFailureComposeAsync("down", backend.Port, throwOnError: false);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static IReadOnlyDictionary<string, string?> ExportEnvironment(
        string endpoint,
        string deploymentMarker
    ) =>
        new Dictionary<string, string?>
        {
            // Standard OTEL_* values intentionally have precedence over the application section.
            // Override inherited workstation values so this opt-in test cannot export off-machine.
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint,
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
            ["OTEL_EXPORTER_OTLP_HEADERS"] = string.Empty,
            ["OTEL_TRACES_EXPORTER"] = "otlp",
            ["OTEL_METRICS_EXPORTER"] = "otlp",
            ["OTEL_LOGS_EXPORTER"] = "otlp",
            ["OTEL_RESOURCE_ATTRIBUTES"] =
                "deployment.environment.name=" + deploymentMarker,
            ["Observability__ExporterEnabled"] = "true",
            ["Observability__Endpoint"] = endpoint,
            ["Observability__ExporterProtocol"] = "http/protobuf",
            ["Observability__ExportTraces"] = "true",
            ["Observability__ExportMetrics"] = "true",
            ["Observability__ExportLogs"] = "true",
            ["Observability__BatchDelayMilliseconds"] = "50",
            ["Observability__ExportTimeoutMilliseconds"] = "1000",
            ["Observability__QueueCapacity"] = "64",
            ["Observability__MaxExportBatchSize"] = "32",
        };

    private static async Task RunDockerAsync(params string[] arguments)
        => await RunDockerAsync(arguments, null, true);

    private static async Task RunDockerAsync(
        string[] arguments,
        IReadOnlyDictionary<string, string?>? environment,
        bool throwOnError
    )
    {
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        if (environment is not null)
            foreach (var item in environment)
                start.Environment[item.Key] = item.Value;
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Docker did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        if (throwOnError && process.ExitCode != 0)
            throw new InvalidOperationException(
                $"docker {string.Join(' ', arguments)} failed: {await stderr}\n{await stdout}"
            );
    }

    private static async Task RunFailureComposeAsync(
        string action,
        int backendPort,
        bool throwOnError = true
    )
    {
        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var observability = Path.Combine(repository, "observability");
        var arguments = new[]
        {
            "compose",
            "--project-name", FailureProject,
            "--project-directory", observability,
            "--file", Path.Combine(observability, "docker-compose.yml"),
            action,
            action == "up" ? "--detach" : string.Empty,
        }.Where(value => value.Length > 0).ToArray();
        await RunDockerAsync(
            arguments,
            new Dictionary<string, string?>
            {
                ["OTEL_COLLECTOR_CONFIG"] = "collector.failure-test.yaml",
                ["OTEL_GRPC_PORT"] = "14317",
                ["OTEL_HTTP_PORT"] = "14318",
                ["OTEL_HEALTH_PORT"] = "14133",
                ["OTEL_METRICS_PORT"] = "18888",
                ["OTEL_TEST_BACKEND_ENDPOINT"] =
                    $"http://host.docker.internal:{backendPort}",
                ["OTEL_BACKEND_ENDPOINT"] = string.Empty,
                ["OTEL_BACKEND_AUTHORIZATION"] = string.Empty,
            },
            throwOnError
        );
    }

    private static async Task<string?> GetStringAsync(string uri)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            return await client.GetStringAsync(uri);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private static IEnumerable<int> QueueSizes(string metrics) =>
        Regex.Matches(metrics, "otelcol_exporter_queue_size\\{[^}]+\\} ([0-9]+)")
            .Select(match => int.Parse(match.Groups[1].Value));

    private static TimeSpan Max(TimeSpan first, TimeSpan second) =>
        first >= second ? first : second;

    private static async Task WaitUntilAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout
    )
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        Exception? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                if (await condition())
                    return;
            }
            catch (Exception error)
            {
                last = error;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("Collector condition was not met.", last);
    }
}

internal enum FailureBackendMode
{
    SlowFailure,
    Healthy,
}

internal sealed class FailureBackend : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Any, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _server;
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _paths = new();
    private int _successfulRequests;

    public FailureBackend()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _server = ServeAsync();
    }

    public int Port { get; }
    public FailureBackendMode Mode { get; set; }
    public int SuccessfulRequests => Volatile.Read(ref _successfulRequests);
    public IReadOnlyCollection<string> Paths => _paths.ToArray();

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
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (SocketException) when (_stop.IsCancellationRequested)
            {
                break;
            }
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
                var requestLine = header.Split("\r\n", StringSplitOptions.None)[0];
                var requestParts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (requestParts.Length >= 2)
                    _paths.Enqueue(requestParts[1]);
                var contentLength = Regex.Match(
                    header,
                    "(?im)^Content-Length:\\s*([0-9]+)"
                );
                if (contentLength.Success)
                {
                    var remaining = int.Parse(contentLength.Groups[1].Value);
                    var buffer = new byte[Math.Min(8192, Math.Max(remaining, 1))];
                    while (remaining > 0)
                    {
                        var read = await stream.ReadAsync(
                            buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                            _stop.Token
                        );
                        if (read == 0)
                            break;
                        remaining -= read;
                    }
                }

                if (Mode == FailureBackendMode.SlowFailure)
                    await Task.Delay(TimeSpan.FromSeconds(1), _stop.Token);
                var healthy = Mode == FailureBackendMode.Healthy;
                if (healthy)
                    Interlocked.Increment(ref _successfulRequests);
                var response = healthy
                    ? "HTTP/1.1 200 OK\r\nContent-Type: application/x-protobuf\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                    : "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _stop.Token);
            }
            catch (Exception) when (_stop.IsCancellationRequested || !client.Connected) { }
            catch (IOException) { }
        }
    }

    private static async Task<string> ReadHeaderAsync(
        NetworkStream stream,
        CancellationToken cancellationToken
    )
    {
        var bytes = new List<byte>(1024);
        var tail = 0;
        while (bytes.Count < 64 * 1024 && tail != 0x0D0A0D0A)
        {
            var buffer = new byte[1];
            if (await stream.ReadAsync(buffer, cancellationToken) == 0)
                break;
            bytes.Add(buffer[0]);
            tail = (tail << 8) | buffer[0];
        }
        return Encoding.ASCII.GetString(bytes.ToArray());
    }
}

[CollectionDefinition("CollectorIntegration", DisableParallelization = true)]
public sealed class CollectorIntegrationCollection;

public sealed class CollectorFactAttribute : FactAttribute
{
    public CollectorFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("AGENT_MEMORY_RUN_COLLECTOR_TESTS") != "1")
            Skip = "Set AGENT_MEMORY_RUN_COLLECTOR_TESTS=1 after starting the local collector profile.";
        else if (string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("AGENT_MEMORY_COLLECTOR_CAPTURE_FILE")
        ))
            Skip = "Set AGENT_MEMORY_COLLECTOR_CAPTURE_FILE to the test overlay output.";
    }
}

public sealed class CollectorFailureFactAttribute : FactAttribute
{
    public CollectorFailureFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("AGENT_MEMORY_RUN_COLLECTOR_FAILURE_TESTS") != "1")
            Skip = "Set AGENT_MEMORY_RUN_COLLECTOR_FAILURE_TESTS=1 when Docker failure injection is available.";
    }
}
