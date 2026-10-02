using Microsoft.Extensions.Configuration;

namespace AgentSession.MCP.Extensions;

internal static class OnuObservabilityCompatibility
{
    public static IConfiguration Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        Map(configuration, values, "Enabled");
        Map(configuration, values, "ExporterEnabled");
        Map(configuration, values, "ExportTraces");
        Map(configuration, values, "ExportMetrics");
        Map(configuration, values, "ExportLogs");
        Map(configuration, values, "ServiceName", "agent-session-mcp");
        Map(configuration, values, "ServiceNamespace", "agent-memory");
        Map(configuration, values, "DeploymentEnvironment");
        Map(configuration, values, "ExporterProtocol");
        Map(configuration, values, "Endpoint");
        Map(configuration, values, "TraceSamplingRatio");
        Map(configuration, values, "ExportTimeoutMilliseconds");
        Map(configuration, values, "BatchDelayMilliseconds");
        Map(configuration, values, "QueueCapacity");
        Map(configuration, values, "MaxExportBatchSize", targetName: "MaximumExportBatchSize");
        Map(configuration, values, "ShutdownFlushTimeoutMilliseconds");
        Map(configuration, values, "MaxLogBodyLength", targetName: "MaximumLogBodyLength");
        Map(configuration, values, "MaxAttributeLength", targetName: "MaximumAttributeLength");
        Map(configuration, values, "LogRateLimitWindowSeconds");
        Map(configuration, values, "LogRateLimitBurst");
        Map(configuration, values, "MaxLogRateLimitKeys", targetName: "MaximumLogRateLimitKeys");
        Map(configuration, values, "MaxToolArgumentFields", targetName: "Mcp:MaximumInputItems");
        Map(configuration, values, "MaxResultContentItems", targetName: "Mcp:MaximumOutputItems");
        Map(configuration, values, "LogRateLimitWindowSeconds", targetName: "Mcp:LogRateLimitWindowSeconds");
        Map(configuration, values, "LogRateLimitBurst", targetName: "Mcp:LogRateLimitBurst");
        Map(configuration, values, "MaxLogRateLimitKeys", targetName: "Mcp:MaximumLogRateLimitKeys");
        MapArray(configuration, values, "InsecureSidecarHosts");

        SetDefault(configuration, values, "LocalJsonLoggingEnabled", "true");
        SetDefault(configuration, values, "Mcp:StdioSafeLogging", "true");

        return new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(values)
            .Build();
    }

    private static void Map(
        IConfiguration source,
        IDictionary<string, string?> target,
        string legacyName,
        string? defaultValue = null,
        string? targetName = null)
    {
        targetName ??= legacyName;
        var newKey = $"OnuObservability:{targetName}";
        if (source[newKey] is not null)
        {
            return;
        }

        var value = source[$"Observability:{legacyName}"] ?? defaultValue;
        if (value is not null)
        {
            target[newKey] = value;
        }
    }

    private static void MapArray(
        IConfiguration source,
        IDictionary<string, string?> target,
        string name)
    {
        if (source.GetSection($"OnuObservability:{name}").GetChildren().Any())
        {
            return;
        }

        foreach (var item in source.GetSection($"Observability:{name}").GetChildren())
        {
            target[$"OnuObservability:{name}:{item.Key}"] = item.Value;
        }
    }

    private static void SetDefault(
        IConfiguration source,
        IDictionary<string, string?> target,
        string name,
        string value)
    {
        var key = $"OnuObservability:{name}";
        if (source[key] is null)
        {
            target[key] = value;
        }
    }
}
