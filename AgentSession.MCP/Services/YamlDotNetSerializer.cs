using AgentSession.MCP.Interfaces;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSession.MCP.Services;

public sealed class YamlDotNetSerializer : IYamlSerializer
{
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public YamlDotNetSerializer()
    {
        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public string Serialize<T>(T value) => _serializer.Serialize(value);

    public T? Deserialize<T>(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return default;
        }

        return _deserializer.Deserialize<T>(yaml);
    }
}
