using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Services;

public static class MemoryClaimHash
{
    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Normalize(NormalizationForm.FormC);

    public static string Content(string content, JsonElement? structuredData) =>
        ManagedTransactionStore.Hash(
            "claim-v1\n"
                + JsonSerializer.Serialize(
                    new
                    {
                        content = Normalize(content),
                        structuredData = structuredData.HasValue
                            ? Canonical(structuredData.Value)
                            : null,
                    },
                    MemoryJson.Options
                )
        );

    public static string Scope(MemoryRecord record) =>
        ManagedTransactionStore.Hash(
            JsonSerializer.Serialize(
                new
                {
                    version = 1,
                    record.RepositoryId,
                    record.Tier,
                    record.Category,
                    record.DecisionArea,
                    session = record.Tier == MemoryTier.Temp ? record.SessionId : null,
                    record.ContentHash,
                },
                MemoryJson.Options
            )
        );

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
