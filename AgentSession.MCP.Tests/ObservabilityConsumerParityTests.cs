using System.Diagnostics;
using System.Text.Json;
using OnuObservability.Mcp;

namespace AgentSession.MCP.Tests;

public sealed class ObservabilityConsumerParityTests
{
    private static readonly string[] SensitiveCanaries =
    [
        "api-key-sk-observability-canary",
        "Bearer observability-token-canary",
        "person-canary@example.test",
        "Server=private-db;Password=observability-canary",
        "C:\\private-canary\\memory.json",
        "actor-id-canary",
        "operation-id-canary",
        "memory-id-canary",
        "session-id-canary",
    ];

    [Fact]
    public async Task PackagedToolBoundaryKeepsStdoutProtocolSafeAndLogsMetadataOnly()
    {
        var root = NewRoot("mcp-package-boundary");
        try
        {
            using var server = new McpProcess(root);
            await server.InitializeAsync();
            await server.CallAsync("list_agent_sessions", new { pageSize = 10 });
            var failed = await server.RequestAsync(
                "tools/call",
                new
                {
                    name = "memory_record_event",
                    arguments = new
                    {
                        request = new
                        {
                            eventId = "private-canary-event-id",
                            eventType = "memory-validated",
                            agentId = "actor-id-canary",
                        },
                    },
                });
            Assert.True(failed.GetProperty("result").GetProperty("isError").GetBoolean());

            var shutdown = Stopwatch.StartNew();
            var stderr = await server.StopAndReadStderrAsync();
            shutdown.Stop();

            Assert.False(server.ForcedTermination);
            Assert.True(shutdown.Elapsed < TimeSpan.FromSeconds(5), shutdown.Elapsed.ToString());
            var records = stderr.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            Assert.NotEmpty(records);
            Assert.All(records, line => JsonDocument.Parse(line).Dispose());
            Assert.Contains(records, line => line.Contains("mcp.tool.completed", StringComparison.Ordinal));
            Assert.Contains(records, line => line.Contains("mcp.tool.failed", StringComparison.Ordinal));
            Assert.DoesNotContain(root, stderr, StringComparison.OrdinalIgnoreCase);
            foreach (var canary in SensitiveCanaries)
            {
                Assert.DoesNotContain(canary, stderr, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task PackagedExporterFailureDoesNotChangeMcpResponseAndIsBounded()
    {
        var root = NewRoot("mcp-package-export-failure");
        try
        {
            using var process = new McpProcess(
                root,
                environment: new Dictionary<string, string?>
                {
                    ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:1",
                    ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
                    ["OTEL_EXPORTER_OTLP_HEADERS"] = string.Empty,
                    ["OTEL_TRACES_EXPORTER"] = "otlp",
                    ["OTEL_METRICS_EXPORTER"] = "otlp",
                    ["OTEL_LOGS_EXPORTER"] = "none",
                    ["Observability__ExporterEnabled"] = "true",
                    ["Observability__Endpoint"] = "http://127.0.0.1:1",
                    ["Observability__ExporterProtocol"] = "http/protobuf",
                    ["Observability__ExportTraces"] = "true",
                    ["Observability__ExportMetrics"] = "true",
                    ["Observability__BatchDelayMilliseconds"] = "10",
                    ["Observability__ExportTimeoutMilliseconds"] = "100",
                    ["Observability__QueueCapacity"] = "8",
                    ["Observability__MaxExportBatchSize"] = "4",
                });
            await process.InitializeAsync();
            var response = await process.RequestAsync("ping", new { });
            Assert.True(response.TryGetProperty("result", out _), response.ToString());
            await Task.Delay(250);

            var stderr = await process.StopAndReadStderrAsync();
            Assert.False(process.ForcedTermination);
            var records = stderr.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            Assert.All(records, line => JsonDocument.Parse(line).Dispose());
            var diagnostics = records.Where(line =>
                line.Contains("mcp.telemetry.dropped", StringComparison.Ordinal)).ToArray();
            Assert.InRange(diagnostics.Length, 1, 4);
            Assert.All(diagnostics, line =>
            {
                Assert.DoesNotContain(root, line, StringComparison.OrdinalIgnoreCase);
                Assert.True(line.Length < 1_024, line);
            });
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static string NewRoot(string prefix) => Path.Combine(
        Path.GetTempPath(),
        prefix,
        Guid.NewGuid().ToString("N"));
}
