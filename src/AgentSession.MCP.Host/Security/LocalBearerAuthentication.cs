using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using AgentSession.MCP.Host.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PlatformHostOptions = AgentSession.MCP.Host.Configuration.HostOptions;

namespace AgentSession.MCP.Host.Security;

public static class HostAuthorizationPolicies
{
    public const string AuthenticatedPolicy = "mcp-authenticated";
    public static string ScopePolicy(string scope) => $"mcp-scope-{Normalize(scope)}";

    public static string Normalize(string value) =>
        new string(value.Trim().ToLowerInvariant().Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
}

public sealed class LocalBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<PlatformHostOptions> hostOptions,
    PreAuthenticationRateLimiter preAuthenticationRateLimiter)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    private const string FailurePermitConsumedItem = "AgentSession.MCP.PreAuthenticationFailurePermitConsumed";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values) || values.Count != 1)
            return await ConsumeFailurePermitAsync(AuthenticateResult.NoResult());

        var header = values[0];
        if (header is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return await ConsumeFailurePermitAsync(AuthenticateResult.Fail("A bearer token is required."));

        var supplied = header[7..].Trim();
        var configured = hostOptions.Value.Authentication.Token;
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(configured));
        if (!matches)
            return await ConsumeFailurePermitAsync(AuthenticateResult.Fail("The bearer token is invalid."));

        var options = hostOptions.Value.Authentication;
        var claims = new List<Claim>
        {
            new("client_id", options.ClientId),
            new(ClaimTypes.NameIdentifier, options.ClientId),
            new("scope", string.Join(' ', options.Scopes))
        };
        if (!string.IsNullOrWhiteSpace(options.TenantId)) claims.Add(new Claim("tenant_id", options.TenantId));
        if (!string.IsNullOrWhiteSpace(options.UserId)) claims.Add(new Claim("sub", options.UserId));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private async Task<AuthenticateResult> ConsumeFailurePermitAsync(AuthenticateResult failure)
    {
        if (Context.Items.ContainsKey(FailurePermitConsumedItem))
            return failure;

        Context.Items[FailurePermitConsumedItem] = true;
        await preAuthenticationRateLimiter.TryAcquireAsync(Context, Context.RequestAborted);
        return failure;
    }
}
