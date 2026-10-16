using AgentSession.MCP.Protocol.Filters;
using AgentSession.MCP.Core.Context;
using AgentSession.MCP.Protocol.Errors;
using AgentSession.MCP.Protocol.Mapping;
using AgentSession.MCP.Protocol.Surfaces.AgentMemory;
using AgentSession.MCP.Protocol.Surfaces.Session;
using AgentSession.MCP.Protocol.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentSession.MCP.Protocol.Registration;

public static class ProtocolRegistration
{
    private static readonly IReadOnlySet<string> ExpectedToolNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "append_agent_memory", "checkpoint_agent_session", "coordinate_agent_task", "create_or_activate_session", "list_agent_sessions", "resume_agent_session",
        "memory_archive", "memory_cleanup", "memory_commit_compaction", "memory_compact", "memory_delete", "memory_get", "memory_list_review", "memory_mark_reviewed",
        "memory_prepare_compaction", "memory_promote", "memory_recall", "memory_rebuild_indexes", "memory_record_event", "memory_record_outcome", "memory_reindex",
        "memory_remember", "memory_retire", "memory_status", "memory_supersede", "memory_update"
    };

    public static IMcpServerBuilder AddAgentSessionProtocol(this IMcpServerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddSingleton<ProtocolBoundsOptions>();
        builder.Services.TryAddScoped<ProtocolAdapterPipeline>();
        builder.WithTools<SessionTools>()
            .WithTools<AgentMemoryTools>()
            .WithRequestFilters(filters =>
            {
                filters.AddListToolsFilter(next => async (context, cancellationToken) =>
                {
                    var result = await next(context, cancellationToken);
                    return CapabilityVisibilityFilter.FilterVisible(result, context.Server.Services!);
                });
                filters.AddCallToolFilter(next => (context, cancellationToken) =>
                    InvokeAuthorizedToolAsync(next, context, cancellationToken));
            });

        builder.Services.AddOptions<McpServerOptions>()
            .Configure(options => options.Capabilities = new ServerCapabilities { Tools = new ToolsCapability() })
            .PostConfigure(options =>
            {
                var names = options.ToolCollection?.PrimitiveNames ?? [];
                if (names.Count != ExpectedToolNames.Count ||
                    names.Distinct(StringComparer.Ordinal).Count() != ExpectedToolNames.Count ||
                    !names.ToHashSet(StringComparer.Ordinal).SetEquals(ExpectedToolNames) ||
                    options.ResourceCollection is not null && !options.ResourceCollection.IsEmpty ||
                    options.PromptCollection is not null && !options.PromptCollection.IsEmpty)
                    throw new InvalidOperationException($"The MCP capability manifest must contain exactly 26 tools and no resources or prompts (tools={names.Count}, resources={options.ResourceCollection?.Count ?? -1}, prompts={options.PromptCollection?.Count ?? -1}).");
            });
        return builder;
    }

    private static async ValueTask<CallToolResult> InvokeAuthorizedToolAsync(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        var services = context.Server.Services!;
        var accessor = services.GetRequiredService<IPlatformRequestContextAccessor>();
        var request = accessor.Current;
        CapabilityAuthorizationFilter.Require(request, context.Params.Name);

        var limiter = services.GetService<ICapabilityRateLimiter>();
        if (limiter is not null && !await limiter.TryAcquireAsync(request, context.Params.Name, cancellationToken))
            return ProtocolResultMapping.Error("rate_limited");

        return await next(context, cancellationToken);
    }
}
