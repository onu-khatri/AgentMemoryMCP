using System.Reflection;
using System.Text.Json;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Core.Errors;
using AgentSession.MCP.Core.Security;
using AgentSession.MCP.Session.Contracts.Capabilities;
using AgentSession.MCP.Session.Contracts.Registration;

namespace AgentSession.MCP.UnitTests;

public sealed class SessionContractShapeTests
{
    [Fact]
    public void Session_capability_surface_is_exactly_six_context_aware_methods()
    {
        var methods = new[]
        {
            typeof(ISessionLifecycleCapabilities),
            typeof(ISessionCoordinationCapabilities),
            typeof(ISessionResumeCapabilities),
        }.SelectMany(type => type.GetMethods()).OrderBy(method => method.Name).ToArray();
        Assert.Equal(["ActivateAsync", "CheckpointAsync", "CoordinateAsync", "ListAsync", "ResumeAsync", "UpdateArtifactsAsync"], methods.Select(method => method.Name));
        Assert.All(methods, method =>
        {
            var parameters = method.GetParameters();
            Assert.Equal(typeof(PlatformRequestContext), parameters[^2].ParameterType);
            Assert.Equal(typeof(CancellationToken), parameters[^1].ParameterType);
            Assert.Equal("cancellationToken", parameters[^1].Name);
        });
    }

    [Fact]
    public void Session_defaults_enums_and_results_are_preserved()
    {
        Assert.Equal(50, new ResumeSessionRequest("session", "actor").PageSize);
        Assert.Null(new ResumeSessionRequest("session", "actor").Cursor);
        Assert.Equal("unverified", new CoordinateTaskRequest
        {
            SessionId = "session",
            ActorId = "actor",
            OperationId = "operation",
            TaskId = "task",
            Action = AgentSession.MCP.Models.TaskAction.Create,
        }.VerificationStatus);
        Assert.Equal(["Context", "Decision", "Finding", "Handoff", "Plan", "Output"], Enum.GetNames<AgentSession.MCP.Models.SessionArtifactKind>());
        var page = new SessionResumePage("snapshot", 1, 0, [], [], false, null);
        Assert.Empty(page.Entries);
        Assert.Null(page.Cursor);
    }

    [Fact]
    public void Session_json_uses_existing_conventions_and_is_strict()
    {
        var json = JsonSerializer.Serialize(new ResumeSessionRequest("session", "actor"), ContractJson.Options);
        Assert.Contains("\"pageSize\": 50", json, StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ResumeSessionRequest>(
            "{\"sessionId\":\"s\",\"actorId\":\"a\",\"unknown\":true}", ContractJson.Options));
    }

    [Fact]
    public void Session_descriptor_has_exact_operation_inventory()
    {
        Assert.Equal(
        ["create_or_activate_session", "list_agent_sessions", "append_agent_memory", "coordinate_agent_task", "resume_agent_session", "checkpoint_agent_session"],
            SessionModuleRegistrationDescriptor.Value.Capabilities.Select(item => item.OperationId));
        Assert.Equal(
            ["managed-transaction-store", "configured-repository-resource", "canonical-session-storage"],
            SessionModuleRegistrationDescriptor.Value.ExternalDependencies
        );
        Assert.Equal(Enum.GetValues<ErrorCategory>(), SessionModuleRegistrationDescriptor.Value.FailureCategories.Order());
        Assert.All(SessionModuleRegistrationDescriptor.Value.Capabilities, capability =>
        {
            Assert.True(capability.Authorization.RequiresAuthenticatedClient);
            Assert.Equal(
                [$"mcp:session:{capability.Access.ToString().ToLowerInvariant()}"],
                capability.Authorization.RequiredScopes);
            Assert.Equal(ResourceAccessRequirement.RepositoryBound, capability.Authorization.ResourceAccess);
        });
        Assert.Equal(
            ["module", "operation", "result.category", "dependency"],
            SessionModuleRegistrationDescriptor.Value.ObservabilityResponsibilities
        );
        Assert.Equal(
            ["unit", "integration", "contract", "architecture", "end-to-end"],
            SessionModuleRegistrationDescriptor.Value.TestLayers
        );
    }
}
