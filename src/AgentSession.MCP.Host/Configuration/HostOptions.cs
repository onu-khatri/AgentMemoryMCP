namespace AgentSession.MCP.Host.Configuration;

public sealed class HostOptions
{
    public const string SectionName = "Host";

    public string Endpoint { get; set; } = "/mcp";
    public int ShutdownTimeoutSeconds { get; set; } = 10;
    public int RateLimitPermitLimit { get; set; } = 32;
    public int RateLimitQueueLimit { get; set; } = 64;
    public int RateLimitWindowSeconds { get; set; } = 1;
    public int PreAuthenticationPermitLimit { get; set; } = 8;
    public int PreAuthenticationWindowSeconds { get; set; } = 60;
    public LocalBearerOptions Authentication { get; set; } = new();

    public sealed class LocalBearerOptions
    {
        public string Token { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string[] Scopes { get; set; } = [];
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
    }

    public static bool IsValid(HostOptions options)
    {
        var authentication = options?.Authentication;
        return options is not null
            && Uri.TryCreate(options.Endpoint, UriKind.Relative, out var endpoint)
            && endpoint is not null
            && options.Endpoint.StartsWith("/", StringComparison.Ordinal)
            && !options.Endpoint.Contains("//", StringComparison.Ordinal)
            && options.ShutdownTimeoutSeconds is > 0 and <= 300
            && options.RateLimitPermitLimit is > 0 and <= 10000
            && options.RateLimitQueueLimit is >= 0 and <= 10000
            && options.RateLimitWindowSeconds is > 0 and <= 60
            && options.PreAuthenticationPermitLimit is > 0 and <= 1000
            && options.PreAuthenticationWindowSeconds is > 0 and <= 60
            && authentication is not null
            && IsStrongToken(authentication.Token)
            && !string.IsNullOrWhiteSpace(authentication.ClientId)
            && authentication.Scopes is { Length: > 0 }
            && authentication.Scopes.All(scope => !string.IsNullOrWhiteSpace(scope));
    }

    public static bool IsStrongToken(string? token) =>
        token is { Length: >= 32 }
        && token.All(character => !char.IsWhiteSpace(character) && !char.IsControl(character));
}
