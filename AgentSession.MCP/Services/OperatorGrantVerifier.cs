using System.Text.Json;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;

namespace AgentSession.MCP.Services;

public enum OperatorAction
{
    DeleteProtectedMemory,
    ArchiveProtectedMemory,
    ValidateLearning,
}

public sealed record OperatorGrant(
    int SchemaVersion,
    string RepositoryId,
    string MemoryId,
    long Revision,
    OperatorAction Action,
    string GrantedBy,
    DateTimeOffset ExpiresAtUtc
);

public sealed class OperatorGrantVerifier(
    ManagedStoragePathResolver resolver,
    MemoryStoragePaths paths,
    TimeProvider time
)
{
    public void Require(MemoryRecord record, OperatorAction action)
    {
        var name = action switch
        {
            OperatorAction.DeleteProtectedMemory => "delete-protected-memory",
            OperatorAction.ArchiveProtectedMemory => "archive-protected-memory",
            OperatorAction.ValidateLearning => "validate-learning",
            _ => throw new ValidationException("Unknown operator action."),
        };
        var path = resolver.ApprovalFile(record.Id, record.Revision, name);
        if (!File.Exists(path) || new FileInfo(path).Length > 16_384)
            throw Missing();
        OperatorGrant? grant;
        try
        {
            grant = JsonSerializer.Deserialize<OperatorGrant>(
                File.ReadAllText(path),
                MemoryJson.Options
            );
        }
        catch (JsonException)
        {
            throw Missing();
        }
        if (
            grant is null
            || grant.SchemaVersion != 1
            || grant.RepositoryId != paths.RepositoryId
            || grant.MemoryId != record.Id
            || grant.Revision != record.Revision
            || grant.Action != action
            || string.IsNullOrWhiteSpace(grant.GrantedBy)
            || grant.ExpiresAtUtc.Offset != TimeSpan.Zero
            || grant.ExpiresAtUtc <= time.GetUtcNow()
        )
            throw Missing();
    }

    private static ValidationException Missing() =>
        new(
            "An unexpired operator-managed grant matching repository, memory revision and action is required.",
            "approval_required"
        );
}
