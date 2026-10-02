using System.Collections.Concurrent;
using System.Diagnostics;
using AgentSession.MCP.Observability;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Extensions;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Models.Memory;
using AgentSession.MCP.Options;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace AgentSession.MCP.Tests;

public sealed class ObservabilityBackgroundTests
{
    [Fact]
    public async Task BackgroundOperationSpansAreCoarseAndSanitizedOnValidationFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), "background-telemetry", Guid.NewGuid().ToString("N"));
        var activities = new ThreadSafeCollection<Activity>();
        try
        {
            using var tracing = Sdk.CreateTracerProviderBuilder()
                .AddSource(TelemetrySchema.ActivitySourceName)
                .AddInMemoryExporter(activities)
                .Build();
            using var provider = new ServiceCollection()
                .AddMemoryStorageConfiguration(
                    new ConfigurationBuilder().AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["SystemStorage:Root"] = root,
                            ["Repository:Id"] = "background-repo",
                        }
                    ).Build()
                )
                .BuildServiceProvider();

            await Assert.ThrowsAsync<ValidationException>(() =>
                provider.GetRequiredService<MemoryReindexService>().ReindexAsync(
                    new("private-operation", MemoryReindexScope.FileSystem, MaxItems: 0)
                )
            );
            await Assert.ThrowsAsync<ValidationException>(() =>
                provider.GetRequiredService<VectorMigrationService>().MigrateAsync("../private-operation")
            );
            await Assert.ThrowsAsync<ValidationException>(() =>
                provider.GetRequiredService<VectorIndexCoordinator>().ReconcileAsync(
                    new(
                        "different-repository",
                        "private-alias",
                        "private-collection",
                        new(
                            Provider: "ollama",
                            Model: "private-model",
                            ModelDigest: "private-digest",
                            Dimension: 3,
                            ProjectionVersion: "v1",
                            Hash: "private-hash"
                        )
                    ),
                    1
                )
            );
            Assert.True(tracing.ForceFlush(5_000));

            var operationSpans = activities.Where(activity =>
                activity.OperationName.StartsWith("mcp operation maintenance ", StringComparison.Ordinal)
            ).ToArray();
            Assert.Equal(3, operationSpans.Length);
            Assert.Equal(
                ["migration", "reconciliation", "reindex"],
                operationSpans
                    .Select(activity => activity.GetTagItem("dependency.operation")?.ToString() ?? string.Empty)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
            );
            Assert.All(operationSpans, activity =>
            {
                Assert.Equal("validation_error", activity.GetTagItem("mcp.status"));
                Assert.Equal(ActivityStatusCode.Error, activity.Status);
            });
            var serialized = string.Join('|', operationSpans.SelectMany(activity => activity.TagObjects).Select(tag => tag.Value));
            Assert.DoesNotContain("private", serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(root, serialized, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task HostedMaintenancePassRecordsOneCoarseSuccessSpan()
    {
        var now = DateTimeOffset.UtcNow;
        var pass = new StubMaintenancePass(_ => Task.FromResult(Result(now)));

        var capture = await RunOnePassAsync(pass);

        var activity = Assert.Single(MaintenanceActivities(capture.Activities));
        Assert.Equal("success", activity.GetTagItem("mcp.status"));
        Assert.Equal(12, activity.GetTagItem("mcp.work.processed"));
        Assert.Equal(9, activity.GetTagItem("mcp.work.indexed"));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Empty(capture.Logs);
    }

    [Fact]
    public async Task HostedMaintenancePassRecordsBoundedPartialAndDeferredWorkOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var pass = new StubMaintenancePass(_ => Task.FromResult(
            Result(now) with
            {
                Errors = 3,
                VectorsDeferred = 7,
                VectorsQueued = 5,
                OrphansDeleted = 2,
                ErrorCode = "maintenance_errors",
            }
        ));

        var capture = await RunOnePassAsync(pass);

        var activity = Assert.Single(MaintenanceActivities(capture.Activities));
        Assert.Equal("dependency_error", activity.GetTagItem("mcp.status"));
        Assert.Equal(7, activity.GetTagItem("mcp.work.deferred"));
        Assert.Equal(5, activity.GetTagItem("mcp.work.queued"));
        Assert.Equal(2, activity.GetTagItem("mcp.work.orphans"));
        Assert.Equal(3, activity.GetTagItem("mcp.work.errors"));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Single(capture.Logs);
    }

    [Fact]
    public async Task HostedMaintenancePassClassifiesDependencyOutageWithoutPerRecordTelemetry()
    {
        var pass = new StubMaintenancePass(_ => throw new HttpRequestException("private endpoint"));

        var capture = await RunOnePassAsync(pass);

        var activity = Assert.Single(MaintenanceActivities(capture.Activities));
        Assert.Equal("dependency_error", activity.GetTagItem("mcp.status"));
        Assert.Equal("dependency_http_error", activity.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Single(capture.Logs);
        Assert.DoesNotContain("private endpoint", capture.Logs[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostedMaintenancePassClassifiesHostCancellationAndStopsPromptly()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pass = new StubMaintenancePass(async cancellationToken =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Result(DateTimeOffset.UtcNow);
        });
        var activities = new ThreadSafeCollection<Activity>();
        using var tracing = Sdk.CreateTracerProviderBuilder()
            .AddSource(TelemetrySchema.ActivitySourceName)
            .AddInMemoryExporter(activities)
            .Build();
        var logger = new CapturingLogger<MemoryMaintenanceService>();
        var service = CreateService(pass, logger);

        await service.StartAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(tracing.ForceFlush(5_000));

        var activity = Assert.Single(MaintenanceActivities(activities));
        Assert.Equal("client_cancelled", activity.GetTagItem("mcp.status"));
        Assert.Equal("client_cancelled", activity.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Empty(logger.Messages);
    }

    private static async Task<(IReadOnlyCollection<Activity> Activities, IReadOnlyList<string> Logs)>
        RunOnePassAsync(IMemoryMaintenancePass pass)
    {
        var activities = new ThreadSafeCollection<Activity>();
        using var tracing = Sdk.CreateTracerProviderBuilder()
            .AddSource(TelemetrySchema.ActivitySourceName)
            .AddInMemoryExporter(activities)
            .Build();
        var logger = new CapturingLogger<MemoryMaintenanceService>();
        var service = CreateService(pass, logger);

        await service.StartAsync(CancellationToken.None);
        await ((StubMaintenancePass)pass).Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(tracing.ForceFlush(5_000));
        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(tracing.ForceFlush(5_000));

        return (activities.ToArray(), logger.Messages);
    }

    private static MemoryMaintenanceService CreateService(
        IMemoryMaintenancePass pass,
        ILogger<MemoryMaintenanceService> logger
    ) => new(
        pass,
        new MemoryMaintenanceState(),
        Microsoft.Extensions.Options.Options.Create(new MemoryPolicyOptions
        {
            Temp = new() { CleanupIntervalMinutes = 60 },
        }),
        TimeProvider.System,
        logger
    );

    private static IEnumerable<Activity> MaintenanceActivities(
        IEnumerable<Activity> activities
    ) => activities.Where(activity =>
        activity.OperationName == "mcp operation maintenance maintenance"
        && Equals(activity.GetTagItem("dependency.type"), "maintenance")
        && Equals(activity.GetTagItem("dependency.operation"), "maintenance")
    );

    private static MemoryMaintenancePassResult Result(DateTimeOffset now) => new(
        now,
        now,
        now,
        now,
        Deleted: 4,
        Errors: 0,
        Archived: 3,
        VectorsProcessed: 12,
        VectorsIndexed: 9,
        VectorsDeferred: 0,
        VectorsQueued: 0,
        OrphansDeleted: 0,
        ErrorCode: null
    );

    private sealed class StubMaintenancePass(
        Func<CancellationToken, Task<MemoryMaintenancePassResult>> action
    ) : IMemoryMaintenancePass
    {
        public TaskCompletionSource Completed { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public async Task<MemoryMaintenancePassResult> RunAsync(
            CancellationToken cancellationToken
        )
        {
            try
            {
                return await action(cancellationToken);
            }
            finally
            {
                Completed.TrySetResult();
            }
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyList<string> Messages => _messages.ToArray();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => _messages.Enqueue(formatter(state, exception));
    }
}
