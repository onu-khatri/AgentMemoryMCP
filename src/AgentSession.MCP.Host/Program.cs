using AgentSession.MCP.AgentMemory.DependencyInjection;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Core.Errors;
using AgentSession.MCP.Host.Configuration;
using AgentSession.MCP.Host.Context;
using AgentSession.MCP.Host.RateLimiting;
using AgentSession.MCP.Host.Security;
using AgentSession.MCP.Host;
using AgentSession.MCP.Infrastructure.Configuration;
using AgentSession.MCP.Infrastructure.DependencyInjection;
using AgentSession.MCP.Protocol.Registration;
using AgentSession.MCP.ServiceDefaults;
using AgentSession.MCP.Session.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using OnuObservability.Hosting.Composition;
using OnuObservability.Mcp;
using PlatformHostOptions = AgentSession.MCP.Host.Configuration.HostOptions;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("agent-session-mcp-http");
builder.Host.ConfigureHostOptions(options =>
{
    var seconds = builder.Configuration.GetValue<int?>($"{PlatformHostOptions.SectionName}:ShutdownTimeoutSeconds") ?? 10;
    options.ShutdownTimeout = TimeSpan.FromSeconds(seconds);
});
var observabilityConfiguration = new ConfigurationBuilder()
    .AddConfiguration(builder.Configuration)
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["OnuObservability:ServiceName"] = "agent-session-mcp-http",
        ["OnuObservability:ServiceNamespace"] = "agent-memory",
        ["OnuObservability:ExporterEnabled"] = "false"
    })
    .Build();
var observability = builder.Services.AddOnuObservability(observabilityConfiguration);

builder.Services
    .AddOptions<PlatformHostOptions>()
    .Bind(builder.Configuration.GetSection(PlatformHostOptions.SectionName))
    .Validate(PlatformHostOptions.IsValid, "Host configuration is invalid. Check endpoint, repository, local bearer and rate-limit settings.")
    .ValidateOnStart();
builder.Services.AddAgentSessionInfrastructure(builder.Configuration);
builder.Services.AddAgentSessionModule(builder.Configuration);
builder.Services.AddAgentMemoryModule(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<HostReadinessCheck>("storage", tags: ["ready"]);
builder.Services.AddScoped<IPlatformRequestContextAccessor, PlatformRequestContextAccessor>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<HostRateLimiter>();
builder.Services.AddSingleton<PreAuthenticationRateLimiter>();
builder.Services.AddSingleton<AgentSession.MCP.Protocol.Filters.ICapabilityRateLimiter>(services => services.GetRequiredService<HostRateLimiter>());

builder.Services
    .AddAuthentication("LocalBearer")
    .AddScheme<AuthenticationSchemeOptions, LocalBearerHandler>("LocalBearer", _ => { });
var hostOptions = builder.Configuration.GetSection(PlatformHostOptions.SectionName).Get<PlatformHostOptions>() ?? new PlatformHostOptions();
var authorization = builder.Services.AddAuthorizationBuilder()
    .AddPolicy(HostAuthorizationPolicies.AuthenticatedPolicy, policy => policy.RequireAuthenticatedUser());
foreach (var scope in hostOptions.Authentication.Scopes ?? [])
    authorization.AddPolicy(HostAuthorizationPolicies.ScopePolicy(scope), policy => policy.RequireClaim("scope", scope));

var mcp = builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
        options.Stateless = true;
    })
    .AddAuthorizationFilters();
mcp.WithOnuObservability(observability);
mcp.AddAgentSessionProtocol();

var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AgentSession.MCP.HttpErrors");
    context.Response.ContentType = "application/json";
    if (error is PlatformException platform && platform.Error.Category == AgentSession.MCP.Core.Errors.ErrorCategory.Forbidden)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = platform.Error.Code });
        return;
    }

    logger.LogError("Unhandled HTTP failure of type {ExceptionType}.", error?.GetType().Name ?? "unknown");
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "internal_error" });
}));
app.Use(async (context, next) =>
{
    context.Response.OnStarting(static state =>
    {
        var httpContext = (HttpContext)state;
        if (httpContext.Items.ContainsKey(HostRateLimiter.RateLimitRejectedItem))
        {
            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            httpContext.Response.Headers.RetryAfter = (httpContext.Items[HostRateLimiter.RetryAfterItem] ?? 60).ToString();
        }
        if (httpContext.Items.ContainsKey(PreAuthenticationRateLimiter.RejectedItem))
        {
            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            httpContext.Response.Headers.RetryAfter = (httpContext.Items[PreAuthenticationRateLimiter.RetryAfterItem] ?? 60).ToString();
        }
        return Task.CompletedTask;
    }, context);
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var accessor = context.RequestServices.GetRequiredService<IPlatformRequestContextAccessor>();
    var repository = context.RequestServices.GetRequiredService<IOptions<RepositoryOptions>>();
    try
    {
        using var scope = RequestContextMapping.Establish(context, accessor, repository);
        await next(context);
    }
    finally
    {
        accessor.Clear();
    }
});
app.MapDefaultEndpoints();
var endpoint = app.Services.GetRequiredService<IOptions<PlatformHostOptions>>().Value.Endpoint;
app.MapMcp(endpoint).RequireAuthorization(HostAuthorizationPolicies.AuthenticatedPolicy);
await app.RunAsync();
