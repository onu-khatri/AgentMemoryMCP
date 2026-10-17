using System.Text.Json;
using AgentSession.MCP.Helpers;

namespace AgentSession.MCP.Protocol.Validation;

public static class ProtocolInputValidation
{
    public static void ValidateRequired<T>(T? value, string name) where T : class
    {
        if (value is null)
            throw new ValidationException($"{name} is required.");
    }

    public static void ValidateRequest<T>(T? value, ProtocolBoundsOptions bounds, string name) where T : class
    {
        ValidateRequired(value, name);
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions
            {
                MaxDepth = bounds.MaxJsonDepth
            });
            if (bytes.Length > bounds.MaxRecordBytes)
                throw new ValidationException("request exceeds the protocol size limit.", "request_too_large");
            using var document = JsonDocument.Parse(bytes);
            ValidateElement(document.RootElement, bounds);
        }
        catch (JsonException)
        {
            throw new ValidationException("request is not valid JSON.", "invalid_json");
        }
    }

    public static void ThrowIfInvalidPage(int pageSize, ProtocolBoundsOptions bounds)
    {
        if (pageSize is < 1 or > 100 || pageSize > bounds.MaxPageSize)
            throw new ValidationException("page size exceeds the protocol limit.", "page_size_exceeded");
    }

    public static void ThrowIfInvalidCollectionCount(int count, ProtocolBoundsOptions bounds)
    {
        if (count < 0 || count > bounds.MaxMutationBatch)
            throw new ValidationException("collection exceeds the protocol limit.", "collection_size_exceeded");
    }

    private static void ValidateElement(JsonElement element, ProtocolBoundsOptions bounds)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String when System.Text.Encoding.UTF8.GetByteCount(element.GetString() ?? string.Empty) > bounds.MaxContentBytes:
                throw new ValidationException("content exceeds the protocol limit.", "content_too_large");
            case JsonValueKind.Array when element.GetArrayLength() > bounds.MaxMutationBatch:
                throw new ValidationException("collection exceeds the protocol limit.", "collection_size_exceeded");
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("pageSize") && property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt32(out var pageSize) && pageSize > bounds.MaxPageSize)
                        throw new ValidationException("page size exceeds the protocol limit.", "page_size_exceeded");
                    if (property.NameEquals("maxResults") && property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt32(out var maxResults) && maxResults > bounds.MaxResults)
                        throw new ValidationException("result count exceeds the protocol limit.", "result_count_exceeded");
                    ValidateElement(property.Value, bounds);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    ValidateElement(item, bounds);
                break;
        }
    }
}
