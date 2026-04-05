using AgentSession.MCP.Helpers;

namespace AgentSession.MCP.Services;

internal static class AgentSessionValidation
{
    public static string ValidateSessionId(string sessionId)
    {
        var sanitized = NameSanitizer.SanitizeIdentifier(sessionId);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ValidationException("sessionId is required and must be filesystem-safe.");
        }

        return sanitized;
    }

    public static string RequireValue(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{fieldName} is required.");
        }

        return value.Trim();
    }
}
