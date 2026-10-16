using System.Diagnostics;
using AgentSession.MCP.Core.Observability;
using AgentSession.MCP.Infrastructure.Observability;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OnuObservability.Mcp.Health;

namespace AgentSession.MCP.IntegrationTests;

public sealed class DependencyOperationTrackerTests
{
    [Fact]
    public async Task SuccessUsesFiniteDimensionsAndBoundsMetadata()
    {
        var activities = new ThreadSafeCollection<Activity>();
        using var tracing = Sdk.CreateTracerProviderBuilder()
            .AddSource(DependencyTelemetrySchema.ActivitySourceName)
            .AddInMemoryExporter(activities)
            .Build();
        var tracker = new DependencyOperationTracker(new RecordingFailureRecorder());

        var result = await tracker.TrackAsync(
            DependencyOperation.External("qdrant", "query", 5_000),
            _ => Task.FromResult(42),
            CancellationToken.None,
            _ => new DependencyOperationMetadata { WorkProcessed = 9_000 }
        );
        Assert.Equal(42, result);
        Assert.True(tracing.ForceFlush(5_000));

        var activity = Assert.Single(activities);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("qdrant", activity.GetTagItem("dependency.type"));
        Assert.Equal("query", activity.GetTagItem("dependency.operation"));
        Assert.Equal(1_024, activity.GetTagItem("mcp.batch.items"));
        Assert.Equal(1_024, activity.GetTagItem("mcp.work.processed"));
        Assert.Equal("success", activity.GetTagItem("mcp.status"));
    }

    [Fact]
    public async Task DependencyFailureIsRecordedAndDoesNotPoisonUnrelatedOperations()
    {
        var failures = new RecordingFailureRecorder();
        var tracker = new DependencyOperationTracker(failures);
        const string privateCanary = "private-provider.example/token/secret";

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            tracker.TrackAsync(
                DependencyOperation.External("ollama", "embedding"),
                _ => throw new HttpRequestException(privateCanary),
                CancellationToken.None
            )
        );
        Assert.Equal(privateCanary, error.Message);
        Assert.Equal(new[] { ("ollama", "embedding") }, failures.Failures);

        var executed = false;
        await tracker.TrackAsync(
            DependencyOperation.Internal("filesystem", "recovery"),
            _ =>
            {
                executed = true;
                return Task.CompletedTask;
            },
            CancellationToken.None
        );
        Assert.True(executed);
        Assert.Single(failures.Failures);
    }

    [Fact]
    public async Task CancellationDeadlineAndFallbackAreNormalizedWithoutSensitiveTags()
    {
        var activities = new ThreadSafeCollection<Activity>();
        using var tracing = Sdk.CreateTracerProviderBuilder()
            .AddSource(DependencyTelemetrySchema.ActivitySourceName)
            .AddInMemoryExporter(activities)
            .Build();
        var failures = new RecordingFailureRecorder();
        var tracker = new DependencyOperationTracker(failures);

        using var callerCancellation = new CancellationTokenSource();
        callerCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            tracker.TrackAsync(
                DependencyOperation.External("qdrant", "query"),
                token => Task.FromCanceled(token),
                callerCancellation.Token
            )
        );
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            tracker.TrackAsync(
                DependencyOperation.External("ollama", "embedding"),
                _ => Task.FromException(new OperationCanceledException("private-timeout-canary")),
                CancellationToken.None
            )
        );
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tracker.TrackAsync(
                DependencyOperation.Internal("private-dependency", "private-operation"),
                _ => throw new InvalidOperationException("private-error-canary"),
                CancellationToken.None
            )
        );
        Assert.True(tracing.ForceFlush(5_000));

        Assert.Contains(
            activities,
            activity =>
                Equals(activity.GetTagItem("mcp.status"), "client_cancelled")
                && Equals(activity.GetTagItem("error.type"), "client_cancelled")
        );
        Assert.Contains(
            activities,
            activity =>
                Equals(activity.GetTagItem("mcp.status"), "deadline_exceeded")
                && Equals(activity.GetTagItem("error.type"), "deadline_exceeded")
        );
        var fallback = Assert.Single(
            activities,
            activity => Equals(activity.GetTagItem("dependency.type"), "other")
        );
        Assert.Equal("other", fallback.GetTagItem("dependency.operation"));
        Assert.Equal("internal_error", fallback.GetTagItem("mcp.status"));
        Assert.Equal("internal_error", fallback.GetTagItem("error.type"));
        Assert.Empty(failures.Failures);

        var exported = string.Join(
            "|",
            activities.SelectMany(activity => activity.TagObjects).Select(tag => tag.Value)
        );
        Assert.DoesNotContain("private-", exported, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingFailureRecorder : IMcpDependencyFailureRecorder
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<(string Type, string Operation)>
            _failures = new();

        public IReadOnlyCollection<(string Type, string Operation)> Failures =>
            _failures.ToArray();

        public void Record(string dependencyType, string dependencyOperation) =>
            _failures.Enqueue((dependencyType, dependencyOperation));
    }
}
