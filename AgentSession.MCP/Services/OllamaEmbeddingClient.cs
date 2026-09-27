using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Options;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

public sealed record EmbeddingBatch(string Model, IReadOnlyList<float[]> Vectors);
public sealed record EmbeddingModelIdentity(string Model, string Digest);

public sealed class OllamaEmbeddingClient(
    HttpClient http,
    IOptions<EmbeddingOptions> options,
    IMemoryContentPolicy policy
)
{
    private static readonly JsonSerializerOptions ExternalJson = new(MemoryJson.Options)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        WriteIndented = false,
    };

    public async Task<EmbeddingModelIdentity> GetModelIdentityAsync(
        CancellationToken cancellationToken = default
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            using var response = await http.GetAsync("api/tags", timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidDataException(
                    $"Ollama model discovery failed with HTTP {(int)response.StatusCode}."
                );
            OllamaTagsResponse payload;
            try
            {
                payload =
                    await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(
                        ExternalJson,
                        timeout.Token
                    ) ?? throw new JsonException("Empty model response.");
            }
            catch (JsonException)
            {
                throw new InvalidDataException("Ollama returned malformed model JSON.");
            }
            var model = payload.Models?.SingleOrDefault(candidate =>
                IsConfiguredModel(candidate.Name, options.Value.Ollama.Model)
                || IsConfiguredModel(candidate.Model, options.Value.Ollama.Model)
            );
            if (model is null || string.IsNullOrWhiteSpace(model.Digest))
                throw new InvalidDataException("Configured Ollama embedding model was not found.");
            return new(
                string.IsNullOrWhiteSpace(model.Model) ? model.Name : model.Model,
                model.Digest
            );
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Ollama model discovery timed out.");
        }
    }

    public async Task<EmbeddingBatch> EmbedAsync(
        IReadOnlyList<string> inputs,
        int? expectedDimensions = null,
        CancellationToken cancellationToken = default
    )
    {
        if (
            inputs.Count == 0
            || inputs.Count > options.Value.MaxBatchSize
            || inputs.Any(string.IsNullOrWhiteSpace)
            || expectedDimensions is <= 0
        )
            throw new ValidationException("Invalid embedding batch or expected dimensions.");
        if (inputs.Sum(input => (long)Encoding.UTF8.GetByteCount(input))
            > options.Value.MaxInputBytes)
            throw new ValidationException(
                "Embedding input exceeds the configured byte limit.",
                "capacity_exceeded"
            );
        policy.Validate(inputs);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            using var response = await http.PostAsJsonAsync(
                "api/embed",
                new
                {
                    model = options.Value.Ollama.Model,
                    input = inputs,
                    truncate = false,
                    dimensions = expectedDimensions,
                },
                MemoryJson.Options,
                timeout.Token
            );
            if (!response.IsSuccessStatusCode)
                throw new InvalidDataException(
                    $"Ollama embedding request failed with HTTP {(int)response.StatusCode}."
                );
            OllamaEmbedResponse payload;
            try
            {
                payload =
                    await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(
                        ExternalJson,
                        timeout.Token
                    ) ?? throw new JsonException("Empty embedding response.");
            }
            catch (JsonException)
            {
                throw new InvalidDataException("Ollama returned malformed embedding JSON.");
            }
            if (
                string.IsNullOrWhiteSpace(payload.Model)
                || !IsConfiguredModel(payload.Model, options.Value.Ollama.Model)
                || payload.Embeddings is null
                || payload.Embeddings.Count != inputs.Count
                || payload.Embeddings.Count == 0
            )
                throw new InvalidDataException(
                    "Ollama returned the wrong embedding batch cardinality."
                );
            var dimension = payload.Embeddings[0]?.Length ?? 0;
            if (
                dimension == 0
                || (expectedDimensions.HasValue && dimension != expectedDimensions.Value)
                || payload.Embeddings.Any(vector =>
                    vector is null
                    || vector.Length != dimension
                    || vector.Any(value => !float.IsFinite(value))
                )
            )
                throw new InvalidDataException(
                    "Ollama returned invalid, non-finite or dimension-mismatched embeddings."
                );
            return new(payload.Model, payload.Embeddings);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Ollama embedding request timed out.");
        }
    }

    private sealed class OllamaEmbedResponse
    {
        public string Model { get; set; } = string.Empty;
        public List<float[]>? Embeddings { get; set; }
    }

    private sealed class OllamaTagsResponse
    {
        public List<OllamaModel>? Models { get; set; }
    }

    private sealed class OllamaModel
    {
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Digest { get; set; } = string.Empty;
    }

    private static bool IsConfiguredModel(string actual, string configured) =>
        string.Equals(actual, configured, StringComparison.Ordinal)
        || string.Equals(actual, configured + ":latest", StringComparison.Ordinal)
        || string.Equals(actual + ":latest", configured, StringComparison.Ordinal);
}
