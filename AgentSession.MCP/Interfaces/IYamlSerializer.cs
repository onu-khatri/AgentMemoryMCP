namespace AgentSession.MCP.Interfaces;

public interface IYamlSerializer
{
    string Serialize<T>(T value);

    T? Deserialize<T>(string yaml);
}
