using System.Collections.Frozen;

namespace AgentSession.MCP.Core.Security;

public enum ResourceAccessRequirement
{
    None,
    RepositoryBound,
}

public sealed record AuthorizationRequirement
{
    public AuthorizationRequirement(
        string capabilityId,
        bool requiresAuthenticatedClient,
        IReadOnlySet<string> requiredScopes,
        ResourceAccessRequirement resourceAccess)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);
        ArgumentNullException.ThrowIfNull(requiredScopes);
        if (requiredScopes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Required scopes cannot contain blank values.", nameof(requiredScopes));

        CapabilityId = capabilityId;
        RequiresAuthenticatedClient = requiresAuthenticatedClient;
        RequiredScopes = requiredScopes.ToFrozenSet(StringComparer.Ordinal);
        ResourceAccess = resourceAccess;
    }

    public string CapabilityId { get; }
    public bool RequiresAuthenticatedClient { get; }
    public IReadOnlySet<string> RequiredScopes { get; }
    public ResourceAccessRequirement ResourceAccess { get; }
}
