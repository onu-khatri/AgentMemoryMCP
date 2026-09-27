using System.Net;
using System.Text;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Options;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Options;

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
    public async Task CallerCancellationAndConfiguredTimeoutRemainDistinct()
    {
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

    private static OllamaEmbeddingClient Create(
        HttpMessageHandler handler,
        double timeoutSeconds = 1,
        int maxInputBytes = 1024
    ) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") },
            Microsoft.Extensions.Options.Options.Create(
                new EmbeddingOptions
                {
                    TimeoutSeconds = timeoutSeconds,
                    MaxInputBytes = maxInputBytes,
                }
            ),
            new MemoryContentPolicy()
        );

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
