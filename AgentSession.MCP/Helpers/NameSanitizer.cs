using System.Text;
using System.Text.RegularExpressions;

namespace AgentSession.MCP.Helpers;

public static partial class NameSanitizer
{
    private static readonly HashSet<string> ReservedNames =
    [
        "con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
    ];

    [GeneratedRegex("[^a-z0-9-]+", RegexOptions.Compiled)]
    private static partial Regex InvalidTokenRegex();

    public static string SanitizeIdentifier(string value)
    {
        var trimmed = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var normalized = InvalidTokenRegex().Replace(trimmed.Replace('_', '-').Replace(' ', '-'), "-");
        normalized = normalized.Trim('-');
        normalized = CollapseDashes(normalized);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return ReservedNames.Contains(normalized) ? $"{normalized}-item" : normalized;
    }

    public static bool IsSafePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Contains("..", StringComparison.Ordinal) || value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        return string.Equals(value, SanitizeIdentifier(value), StringComparison.Ordinal);
    }

    private static string CollapseDashes(string input)
    {
        var sb = new StringBuilder(input.Length);
        var lastDash = false;

        foreach (var ch in input)
        {
            if (ch == '-')
            {
                if (!lastDash)
                {
                    sb.Append(ch);
                    lastDash = true;
                }

                continue;
            }

            sb.Append(ch);
            lastDash = false;
        }

        return sb.ToString();
    }
}
