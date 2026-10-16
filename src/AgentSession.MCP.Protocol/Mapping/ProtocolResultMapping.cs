using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace AgentSession.MCP.Protocol.Mapping;

public static class ProtocolResultMapping
{
    public static CallToolResult Error(string category) => new() { IsError = true, StructuredContent = JsonSerializer.SerializeToElement(new { error = category }) };

    public static T RequireResult<T>(T result) where T : class => result ?? throw new InvalidOperationException("empty_result");
}
