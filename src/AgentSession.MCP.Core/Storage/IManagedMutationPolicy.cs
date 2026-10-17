namespace AgentSession.MCP.Core.Storage;

public interface IManagedMutationPolicy
{
    void Validate(IReadOnlyList<ManagedMutation> mutations);
}
