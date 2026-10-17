using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Options;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

/// <summary>Applies bounded request and conservative sensitive-content validation to Session inputs.</summary>
public sealed class SessionRequestValidator(IOptions<SessionLimitsOptions> options)
{
    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwd", "pwd", "apikey", "api_key", "api-key", "secret",
        "clientsecret", "client_secret", "access_token", "accesstoken", "refresh_token",
        "refreshtoken", "authorization", "privatekey", "private_key",
    };

    private static readonly Regex SecretPattern = new(
        @"-----BEGIN\s+(?:RSA\s+|EC\s+|OPENSSH\s+)?PRIVATE KEY-----|\bAuthorization\s*[:=]\s*(?:Bearer|Basic)\s+[A-Za-z0-9._~+/=-]+|\bBearer\s+[A-Za-z0-9._~+/=-]+|\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[A-Z0-9]{16}|sk-[A-Za-z0-9_-]{20,})\b|\b(?:password|passwd|pwd|api[_-]?key|client[_-]?secret|access[_-]?token|refresh[_-]?token)\s*[=:]\s*[^\s;,]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));

    public void ValidateBatch(int count)
    {
        if (count < 1 || count > options.Value.MaxMutationBatch)
            throw new ValidationException(
                "Record batch exceeds the configured limit.",
                "capacity_exceeded"
            );
    }

    public void Validate<T>(T request, bool content = false)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request, SessionJson.Options);
        if (bytes.Length > (content ? options.Value.MaxContentBytes : options.Value.MaxRecordBytes))
            throw new ValidationException(
                "Input exceeds the configured byte limit.",
                "capacity_exceeded"
            );
        try
        {
            using var document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions { MaxDepth = options.Value.MaxJsonDepth }
            );
            Inspect(document.RootElement);
        }
        catch (JsonException)
        {
            throw new ValidationException("Input exceeds the configured JSON nesting limit.");
        }
    }

    private static void Inspect(JsonElement element, int depth = 0)
    {
        if (depth > 32)
            throw new ValidationException("Content policy nesting limit exceeded.");
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (SecretPattern.IsMatch(property.Name))
                        Reject();
                    if (SecretKeys.Contains(property.Name)
                        && property.Value.ValueKind != JsonValueKind.Null
                        && !(property.Value.ValueKind == JsonValueKind.String
                            && string.IsNullOrWhiteSpace(property.Value.GetString())))
                        Reject();
                    Inspect(property.Value, depth + 1);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    Inspect(item, depth + 1);
                break;
            case JsonValueKind.String:
                try
                {
                    var value = element.GetString()!;
                    if (SecretPattern.IsMatch(value))
                        Reject();
                    InspectNested(value, depth);
                    InspectEncoded(value, depth);
                }
                catch (RegexMatchTimeoutException)
                {
                    throw new ValidationException("Content policy could not safely inspect input.");
                }
                break;
        }
    }

    private static void InspectNested(string value, int depth)
    {
        var text = value.TrimStart();
        if (!text.StartsWith('{') && !text.StartsWith('['))
            return;
        try
        {
            using var nested = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
            Inspect(nested.RootElement, depth + 1);
        }
        catch (JsonException)
        {
            // Ordinary prose that resembles JSON is still covered by the text policy.
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
            InspectNested(decoded, depth);
        }
        catch (Exception error)
            when (error is FormatException or System.Text.DecoderFallbackException or JsonException)
        {
            // Not a supported encoded text payload.
        }
    }

    private static void Reject() => throw new ValidationException(
        "Content rejected by sensitive-data policy; remove secret material before retrying.",
        "content_policy_rejected"
    );
}
