using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;

namespace AgentSession.MCP.Services;

/// <summary>Rejects common secret patterns. Detection is deliberately conservative, not proof that text is secret-free.</summary>
public sealed class MemoryContentPolicy : IMemoryContentPolicy
{
    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    { "password", "passwd", "pwd", "apikey", "api_key", "api-key", "secret", "clientsecret", "client_secret",
      "access_token", "accesstoken", "refresh_token", "refreshtoken", "authorization", "privatekey", "private_key" };

    private static readonly Regex SecretPattern = new(
        @"-----BEGIN\s+(?:RSA\s+|EC\s+|OPENSSH\s+)?PRIVATE KEY-----|\bAuthorization\s*[:=]\s*(?:Bearer|Basic)\s+[A-Za-z0-9._~+/=-]+|\bBearer\s+[A-Za-z0-9._~+/=-]+|\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[A-Z0-9]{16}|sk-[A-Za-z0-9_-]{20,})\b|\b(?:password|passwd|pwd|api[_-]?key|client[_-]?secret|access[_-]?token|refresh[_-]?token)\s*[=:]\s*[^\s;,]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));

    public void Validate<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, MemoryJson.Options);
        Inspect(element);
    }

    private static void Inspect(JsonElement element, int depth = 0)
    {
        if (depth > 32) throw new ValidationException("Content policy nesting limit exceeded.");
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (SecretPattern.IsMatch(property.Name)) Reject();
                    if (SecretKeys.Contains(property.Name) && property.Value.ValueKind != JsonValueKind.Null
                        && !(property.Value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(property.Value.GetString())))
                        Reject();
                    Inspect(property.Value, depth + 1);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) Inspect(item, depth + 1);
                break;
            case JsonValueKind.String:
                try
                {
                    var value = element.GetString()!;
                    if (SecretPattern.IsMatch(value)) Reject();
                    var text = value.TrimStart();
                    if (text.StartsWith('{') || text.StartsWith('['))
                    {
                        JsonDocument? nested = null;
                        try { nested = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 }); }
                        catch (JsonException) { /* ordinary prose is still scanned by the text policy */ }
                        using (nested) { if (nested is not null) Inspect(nested.RootElement, depth + 1); }
                    }
                    InspectEncoded(value, depth);
                }
                catch (RegexMatchTimeoutException)
                {
                    throw new ValidationException("Content policy could not safely inspect input.");
                }
                break;
        }
    }

    private static void InspectEncoded(string value, int depth)
    {
        if (value.Contains('%'))
        {
            try
            {
                var decoded = Uri.UnescapeDataString(value);
                if (decoded != value && SecretPattern.IsMatch(decoded))
                    Reject();
            }
            catch (UriFormatException) { }
        }
        var compact = value.Trim();
        if (compact.Length is < 16 or > 131_072)
            return;
        var normalized = compact.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight((normalized.Length + 3) / 4 * 4, '=');
        try
        {
            var bytes = Convert.FromBase64String(normalized);
            var decoded = new System.Text.UTF8Encoding(false, true).GetString(bytes);
            if (SecretPattern.IsMatch(decoded))
                Reject();
            var trimmed = decoded.TrimStart();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                using var nested = JsonDocument.Parse(
                    trimmed,
                    new JsonDocumentOptions { MaxDepth = Math.Max(1, 32 - depth) }
                );
                Inspect(nested.RootElement, depth + 1);
            }
        }
        catch (Exception error)
            when (error is FormatException or System.Text.DecoderFallbackException or JsonException)
        { /* Not a supported encoded text payload. */ }
    }

    private static void Reject() => throw new ValidationException(
        "Content rejected by sensitive-data policy; remove secret material before retrying.",
        "content_policy_rejected"
    );
}
