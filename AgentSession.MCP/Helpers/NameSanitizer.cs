namespace AgentSession.MCP.Helpers;

/// <summary>Strict portable identifier validation. Supplied identifiers are never transformed.</summary>
public static class NameSanitizer
{
    private static readonly HashSet<string> ReservedNames =
    [
        "con",
        "prn",
        "aux",
        "nul",
        "com1",
        "com2",
        "com3",
        "com4",
        "com5",
        "com6",
        "com7",
        "com8",
        "com9",
        "lpt1",
        "lpt2",
        "lpt3",
        "lpt4",
        "lpt5",
        "lpt6",
        "lpt7",
        "lpt8",
        "lpt9",
    ];

    public static bool IsSafePathSegment(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= 128
        && value[0] != '-'
        && value[^1] != '-'
        && !value.Contains("--", StringComparison.Ordinal)
        && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')
        && !ReservedNames.Contains(value);
}
