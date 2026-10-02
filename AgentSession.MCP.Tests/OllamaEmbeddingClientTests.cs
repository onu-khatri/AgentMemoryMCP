using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AgentSession.MCP.Extensions;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Options;
using AgentSession.MCP.Observability;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OnuObservability.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace AgentSession.MCP.Tests;

public sealed class OllamaEmbeddingClientTests
{
    [Fact]
    public async Task SingleAndBatchRequestsDisableTruncationAndPreserveOrder()
    {
        var bodies = new List<string>();
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var count = bodies.Count == 1 ? 1 : 2;
            var vectors = string.Join(",", Enumerable.Repeat("[1.0,2.0,3.0]", count));
            return Json($"{{\"model\":\"embeddinggemma\",\"embeddings\":[{vectors}]}}");
        });
        var client = Create(handler);

        var single = await client.EmbedAsync(["one"], 3);
        var batch = await client.EmbedAsync(["one", "two"], 3);

        Assert.Single(single.Vectors);
        Assert.Equal(2, batch.Vectors.Count);
        Assert.All(bodies, body => Assert.Contains("\"truncate\": false", body));
        Assert.All(bodies, body => Assert.Contains("\"dimensions\": 3", body));
        Assert.Contains("\"input\": [", bodies[1]);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"model\":\"embeddinggemma\",\"embeddings\":[[1e999,2]]}")]
    [InlineData("{\"model\":\"embeddinggemma\",\"embeddings\":[[1,2],[1,2,3]]}")]
    public async Task InvalidResponsesAreRejected(string response)
    {
        var client = Create(
            new StubHandler((_, _) => Task.FromResult(Json(response)))
        );
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.EmbedAsync(response.Contains("],[", StringComparison.Ordinal) ? ["a", "b"] : ["a"])
        );
    }

    [Fact]
    public async Task ExpectedDimensionMismatchIsRejected()
    {
        var client = Create(
            new StubHandler((_, _) =>
                Task.FromResult(
                    Json("{\"model\":\"embeddinggemma\",\"embeddings\":[[1,2]]}")
                )
            )
        );
        await Assert.ThrowsAsync<InvalidDataException>(() => client.EmbedAsync(["a"], 3));
    }

    [Fact]
    public async Task InvalidDependencyResponseUsesHttpDependencyClassification()
    {
        var activities = new ThreadSafeCollection<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(TelemetrySchema.ActivitySourceName)
            .AddInMemoryExporter(activities)
            .Build();
        var client = Create(new StubHandler((_, _) => Task.FromResult(Json("not-json"))));

        await Assert.ThrowsAsync<InvalidDataException>(() => client.EmbedAsync(["safe-input"]));
        Assert.True(provider.ForceFlush(5_000));

        var dependency = Assert.Single(activities, activity =>
            activity.OperationName == "mcp dependency ollama embedding");
        Assert.Equal("dependency_error", dependency.GetTagItem("mcp.status"));
        Assert.Equal("dependency_http_error", dependency.GetTagItem("error.type"));
        Assert.Empty(dependency.Events);
    }

    [Fact]
    public async Task CallerCancellationAndConfiguredTimeoutRemainDistinct()
    {
        var activities = new ThreadSafeCollection<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(TelemetrySchema.ActivitySourceName)
            .AddInMemoryExporter(activities)
            .Build();
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException();
        });
        var client = Create(handler, timeoutSeconds: 0.02);
        await Assert.ThrowsAsync<TimeoutException>(() => client.EmbedAsync(["timeout"]));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.EmbedAsync(["cancel"], cancellationToken: cancellation.Token)
        );
        Assert.True(provider.ForceFlush(5_000));
        var dependencies = activities.Where(activity =>
            activity.OperationName == "mcp dependency ollama embedding").ToArray();
        Assert.Contains(dependencies, activity =>
            Equals(activity.GetTagItem("mcp.status"), "deadline_exceeded"));
        Assert.Contains(dependencies, activity =>
            Equals(activity.GetTagItem("mcp.status"), "client_cancelled"));
        Assert.All(dependencies, activity =>
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Empty(activity.Events);
        });
    }

    [Fact]
    public async Task SecretAndOversizedInputsAreRejectedBeforeHttp()
    {
        var calls = 0;
        var client = Create(
            new StubHandler((_, _) =>
            {
                calls++;
                return Task.FromResult(Json("{}"));
            }),
            maxInputBytes: 8
        );
        await Assert.ThrowsAsync<ValidationException>(() =>
            client.EmbedAsync(["password=hunter2"])
        );
        await Assert.ThrowsAsync<ValidationException>(() =>
            client.EmbedAsync(["more than eight bytes"])
        );
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ModelDiscoveryReturnsPinnedDigestAndRejectsResponseFromAnotherModel()
    {
        var client = Create(
            new StubHandler((request, _) =>
                Task.FromResult(
                    request.Method == HttpMethod.Get
                        ? Json(
                            "{\"models\":[{\"name\":\"embeddinggemma:latest\",\"model\":\"embeddinggemma:latest\",\"digest\":\"digest-one\"}]}"
                        )
                        : Json("{\"model\":\"another-model\",\"embeddings\":[[1,2,3]]}")
                )
            )
        );

        var identity = await client.GetModelIdentityAsync();

        Assert.Equal("embeddinggemma:latest", identity.Model);
        Assert.Equal("digest-one", identity.Digest);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.EmbedAsync(["claim"], 3));
    }

    [Fact]
    public async Task DependencyAndHttpSpansHaveSafeParentageAndNoContentOrUrl()
    {
        var activities = new ThreadSafeCollection<Activity>();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OnuObservability:ServiceName"] = "agent-memory-test",
                ["OnuObservability:ExporterEnabled"] = "false",
            })
            .Build();
        services.AddOnuObservability(configuration).AddAgentMemoryTelemetry();
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
            .AddSource(TelemetrySchema.ActivitySourceName)
            .AddInMemoryExporter(activities));
        using var root = services.BuildServiceProvider();
        var provider = root.GetRequiredService<TracerProvider>();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var server = ServeOnceAsync(
            listener,
            "{\"model\":\"embeddinggemma\",\"embeddings\":[[1,2,3]]}"
        );
        var client = Create(
            new HttpClientHandler { UseProxy = false },
            baseAddress: new Uri($"http://127.0.0.1:{endpoint.Port}/")
        );

        await client.EmbedAsync(["private-canary-input"], 3);
        await server;
        Assert.True(provider.ForceFlush(5_000));

        var dependency = Assert.Single(activities, activity =>
            activity.OperationName == "mcp dependency ollama embedding");
        var http = Assert.Single(activities, activity => activity.Source.Name == "System.Net.Http");
        Assert.Equal(dependency.TraceId, http.TraceId);
        Assert.Equal(dependency.SpanId, http.ParentSpanId);
        Assert.Equal("ollama", dependency.GetTagItem("dependency.type"));
        Assert.Equal("embedding", dependency.GetTagItem("dependency.operation"));
        Assert.Equal(1, dependency.GetTagItem("mcp.batch.items"));
        Assert.Equal("success", dependency.GetTagItem("mcp.status"));
        var exported = string.Join(
            "|",
            activities.SelectMany(activity => activity.TagObjects)
                .Select(tag => tag.Key + "=" + tag.Value)
        );
        Assert.DoesNotContain("private-canary", exported, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api/embed", exported, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("embeddinggemma", exported, StringComparison.OrdinalIgnoreCase);
    }

    private static OllamaEmbeddingClient Create(
        HttpMessageHandler handler,
        double timeoutSeconds = 1,
        int maxInputBytes = 1024,
        Uri? baseAddress = null
    ) =>
        new(
            new HttpClient(handler)
            {
                BaseAddress = baseAddress ?? new Uri("http://localhost:11434/"),
            },
            Microsoft.Extensions.Options.Options.Create(
                new EmbeddingOptions
                {
                    TimeoutSeconds = timeoutSeconds,
                    MaxInputBytes = maxInputBytes,
                }
            ),
            new MemoryContentPolicy(),
            new McpDependencyTelemetry(new TestDependencyFailureRecorder())
        );

    private static async Task ServeOnceAsync(TcpListener listener, string json)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        using var reader = new StreamReader(
            stream,
            Encoding.ASCII,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true
        );
        var contentLength = 0;
        while (await reader.ReadLineAsync() is { } line && line.Length > 0)
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                contentLength = int.Parse(line["Content-Length:".Length..].Trim());
        }
        if (contentLength > 0)
        {
            var body = new char[contentLength];
            var offset = 0;
            while (offset < body.Length)
            {
                var read = await reader.ReadAsync(body.AsMemory(offset));
                if (read == 0)
                    break;
                offset += read;
            }
        }
        var payload = Encoding.UTF8.GetBytes(json);
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n"
        );
        await stream.WriteAsync(headers);
        await stream.WriteAsync(payload);
    }

    private static HttpResponseMessage Json(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => send(request, cancellationToken);
    }
}
