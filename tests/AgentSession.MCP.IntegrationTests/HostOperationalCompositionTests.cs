using System.Security.Claims;
using AgentSession.MCP.Core.Context;
using Microsoft.AspNetCore.Http;

namespace AgentSession.MCP.IntegrationTests;

public sealed class HostOperationalCompositionTests
{
    [Fact]
    public void RequestContextAccessor_ClearsAndRejectsAccessAfterScope()
    {
        var accessor = new PlatformRequestContextAccessor();
        var context = new PlatformRequestContext(
            new ClientIdentity("integration-client"),
            "tenant-a",
            "user-a",
            new HashSet<string>(["mcp"], StringComparer.Ordinal),
            new RepositoryResource("repository-a"),
            "correlation-a");

        accessor.Set(context);
        Assert.Same(context, accessor.Current);
        accessor.Clear();
        var error = Assert.Throws<AgentSession.MCP.Core.Errors.PlatformException>(() => _ = accessor.Current);
        Assert.Equal("request_context_unset", error.Error.Code);
    }

    [Fact]
    public void RateLimiter_IsolatesClientTenantAndCapability()
    {
        var first = new DefaultHttpContext();
        first.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("client_id", "client-a"),
            new Claim("tenant_id", "tenant-a")], "test"));
        first.Request.Path = "/mcp";
        first.SetEndpoint(new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "mcp"));

        var second = new DefaultHttpContext();
        second.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("client_id", "client-b"),
            new Claim("tenant_id", "tenant-a")], "test"));
        second.Request.Path = "/mcp";
        second.SetEndpoint(first.GetEndpoint());

        var firstContext = new PlatformRequestContext(
            new ClientIdentity("client-a"), "tenant-a", null, new HashSet<string>(), new RepositoryResource("repo"), "corr");
        var secondContext = new PlatformRequestContext(
            new ClientIdentity("client-b"), "tenant-a", null, new HashSet<string>(), new RepositoryResource("repo"), "corr");

        Assert.Equal(ResolvePartitionKey(firstContext, "memory_status"), ResolvePartitionKey(firstContext, "memory_recall"));
        Assert.NotEqual(ResolvePartitionKey(firstContext, "memory_status"), ResolvePartitionKey(secondContext, "memory_status"));
        Assert.Contains("client-a|tenant-a", ResolvePartitionKey(firstContext, "memory_status"));
    }

    private static string ResolvePartitionKey(PlatformRequestContext context, string operationId) =>
        string.Join('|', Normalize(context.Client.ClientId), Normalize(context.TenantId ?? "none"));

    private static string Normalize(string value)
    {
        var normalized = new string(value.Trim().ToLowerInvariant().Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-').ToArray());
        return normalized.Length <= 64 ? normalized : normalized[..64];
    }
}
