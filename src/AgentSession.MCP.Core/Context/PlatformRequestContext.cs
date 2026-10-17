using System.Collections.Frozen;

namespace AgentSession.MCP.Core.Context;

public sealed record ClientIdentity
{
    public ClientIdentity(string clientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ClientId = clientId;
    }

    public string ClientId { get; }
}

public sealed record RepositoryResource
{
    public RepositoryResource(string repositoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        RepositoryId = repositoryId;
    }

    public string RepositoryId { get; }
}

public sealed record PlatformRequestContext
{
    public const int MaxCorrelationIdLength = 128;
    public PlatformRequestContext(
        ClientIdentity client,
        string? tenantId,
        string? userId,
        IReadOnlySet<string> scopes,
        RepositoryResource repository,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(repository);
        if (!IsValidCorrelationId(correlationId))
            throw new ArgumentException("Correlation ID is invalid or exceeds the supported length.", nameof(correlationId));
        if (scopes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Scopes cannot contain blank values.", nameof(scopes));

        Client = client;
        TenantId = tenantId;
        UserId = userId;
        Scopes = scopes.ToFrozenSet(StringComparer.Ordinal);
        Repository = repository;
        CorrelationId = correlationId;
    }

    public ClientIdentity Client { get; }
    public string? TenantId { get; }
    public string? UserId { get; }
    public IReadOnlySet<string> Scopes { get; }
    public RepositoryResource Repository { get; }
    public string CorrelationId { get; }

    public static bool IsValidCorrelationId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaxCorrelationIdLength
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':' or '/');
}
