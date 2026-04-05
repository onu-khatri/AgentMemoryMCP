using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;

namespace AgentSession.MCP.Tools;

/// <summary>
/// MCP tools for local agent session management and persistence.
/// </summary>
public sealed partial class AgentSessionTools
{
    private readonly IAgentSessionService _service;

    public AgentSessionTools(IAgentSessionService service)
    {
        _service = service;
    }

    private static InvalidOperationException ToInvalidOperation(ValidationException ex)
        => new($"Invalid request: {ex.Message}", ex);

    private static async Task<T> WithValidationHandling<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ValidationException ex)
        {
            throw ToInvalidOperation(ex);
        }
    }
}
