using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Services;

public sealed record EmbeddingFingerprint(
    string Provider,
    string Model,
    string? ModelDigest,
    int Dimension,
    string ProjectionVersion,
    string Hash
);

public sealed record VectorCollectionIdentity(
    string RepositoryId,
    string Alias,
    string PhysicalCollection,
    EmbeddingFingerprint Fingerprint
);

public static class EmbeddingProjection
{
    public const string Version = "projection-v1";

    public static string Project(MemoryRecord record)
    {
        if (record.Tier != MemoryTier.Long)
            throw new ValidationException("Only long-term memory has an embedding projection.");
        var structured = record.StructuredData.HasValue
            ? Canonical(record.StructuredData.Value)?.ToJsonString(
                new JsonSerializerOptions { WriteIndented = false }
            )
            : string.Empty;
        return string.Join(
            "\n",
            new[]
            {
                "projection=" + Version,
                "title=" + Normalize(record.Title),
                "category=" + Normalize(record.Category),
                "decisionArea=" + Normalize(record.DecisionArea ?? string.Empty),
                "tags=" + string.Join(",", record.Tags.Select(Normalize).Order(StringComparer.Ordinal)),
                "content=" + Normalize(record.Content),
                "structuredData=" + structured,
            }
        );
    }

    public static EmbeddingFingerprint Fingerprint(
        string provider,
        string model,
        string? modelDigest,
        int dimension,
        string projectionVersion = Version
    )
    {
        if (
            string.IsNullOrWhiteSpace(provider)
            || string.IsNullOrWhiteSpace(model)
            || string.IsNullOrWhiteSpace(projectionVersion)
            || dimension <= 0
        )
            throw new ValidationException("Embedding fingerprint is incomplete.");
        var identity = JsonSerializer.Serialize(
            new
            {
                provider = Normalize(provider),
                model = Normalize(model),
                modelDigest = modelDigest is null ? null : Normalize(modelDigest),
                dimension,
                projectionVersion = Normalize(projectionVersion),
            },
            new JsonSerializerOptions(MemoryJson.Options) { WriteIndented = false }
        );
        return new(
            provider,
            model,
            modelDigest,
            dimension,
            projectionVersion,
            ManagedTransactionStore.Hash(identity)
        );
    }

    public static VectorCollectionIdentity Collection(
        string prefix,
        string repositoryId,
        EmbeddingFingerprint fingerprint
    )
    {
        ManagedStoragePathResolver.RequireIdentifier(repositoryId);
        if (
            string.IsNullOrWhiteSpace(prefix)
            || prefix.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')
            )
        )
            throw new ValidationException("Invalid vector collection prefix.");
        var alias = prefix + "_" + repositoryId;
        return new(repositoryId, alias, alias + "_" + fingerprint.Hash[..16], fingerprint);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n").Replace('\r', '\n').Normalize(NormalizationForm.FormC);

    private static JsonNode? Canonical(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Object => new JsonObject(
                value
                    .EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => new KeyValuePair<string, JsonNode?>(
                        Normalize(property.Name),
                        Canonical(property.Value)
                    ))
            ),
            JsonValueKind.Array => new JsonArray(
                value.EnumerateArray().Select(Canonical).ToArray()
            ),
            JsonValueKind.String => JsonValue.Create(Normalize(value.GetString()!)),
            JsonValueKind.Null => null,
            _ => JsonNode.Parse(value.GetRawText()),
        };
}
