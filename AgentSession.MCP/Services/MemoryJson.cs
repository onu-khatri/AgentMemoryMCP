using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.ComponentModel.DataAnnotations;

namespace AgentSession.MCP.Services;

public static class MemoryJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            MaxDepth = 32,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false)
        );
        options.MakeReadOnly();
        return options;
    }

    public static JsonNode SchemaFor<T>()
    {
        var schema = Options.GetJsonSchemaAsNode(typeof(T));
        ApplyDeclaredBounds(typeof(T), schema);
        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        return schema;
    }

    private static void ApplyDeclaredBounds(Type type, JsonNode? schema)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (schema is not JsonObject node)
            return;
        if (TryElementType(type, out var elementType))
        {
            ApplyDeclaredBounds(elementType, node["items"]);
            return;
        }
        if (node["properties"] is not JsonObject properties)
            return;
        foreach (var property in type.GetProperties())
        {
            var name = Options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
            if (properties[name] is not JsonObject propertySchema)
                continue;
            if (property.GetCustomAttributes(typeof(RangeAttribute), true).SingleOrDefault()
                is RangeAttribute range)
            {
                propertySchema["minimum"] = JsonValue.Create(Convert.ToDouble(range.Minimum));
                propertySchema["maximum"] = JsonValue.Create(Convert.ToDouble(range.Maximum));
            }
            if (property.GetCustomAttributes(typeof(MinLengthAttribute), true).SingleOrDefault()
                is MinLengthAttribute minimum)
                propertySchema[property.PropertyType == typeof(string) ? "minLength" : "minItems"] = minimum.Length;
            if (property.GetCustomAttributes(typeof(MaxLengthAttribute), true).SingleOrDefault()
                is MaxLengthAttribute maximum)
                propertySchema[property.PropertyType == typeof(string) ? "maxLength" : "maxItems"] = maximum.Length;
            ApplyDeclaredBounds(property.PropertyType, propertySchema);
        }
    }

    private static bool TryElementType(Type type, out Type elementType)
    {
        if (type != typeof(string))
        {
            if (type.IsArray)
            {
                elementType = type.GetElementType()!;
                return true;
            }
            var enumerable = type.GetInterfaces()
                .Append(type)
                .FirstOrDefault(candidate =>
                    candidate.IsGenericType
                    && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                );
            if (enumerable is not null)
            {
                elementType = enumerable.GetGenericArguments()[0];
                return true;
            }
        }
        elementType = typeof(object);
        return false;
    }
}
