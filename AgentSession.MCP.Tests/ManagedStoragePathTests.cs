using System.Diagnostics;
using AgentSession.MCP.Extensions;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSession.MCP.Tests;

public sealed class ManagedStoragePathTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "memory-path-tests", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;
    private readonly ManagedStoragePathResolver _paths;

    public ManagedStoragePathTests()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SystemStorage:Root"] = _root, ["Repository:Id"] = "repo-a"
        }).Build();
        _services = new ServiceCollection().AddMemoryStorageConfiguration(config).BuildServiceProvider();
        _paths = _services.GetRequiredService<ManagedStoragePathResolver>();
    }

    [Fact]
    public void ValidPathsAreScopedWithoutCreatingFiles()
    {
        Assert.Equal(Path.Combine(_root, "sessions", "repo-a", "session-one", "coordination", "context.json"),
            _paths.SessionFile("repo-a", "session-one", "coordination", "context.json"));
        Assert.Equal(Path.Combine(_root, "repositories", "repo-a", "AiLearning", "temp", ".index.json"),
            _paths.LearningFile("repo-a", "temp", ".index.json"));
        Assert.False(Directory.Exists(_root));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../other")]
    [InlineData("..\\other")]
    [InlineData("/outside")]
    [InlineData("C:\\outside")]
    [InlineData("record.json:stream")]
    [InlineData("con.json")]
    [InlineData("record.")]
    public void RejectsEscapesAndDeviceNames(string component)
        => Assert.Throws<ValidationException>(() => _paths.LearningFile("repo-a", component));

    [Fact]
    public void RejectsOtherRepositoryAndUnsafeSession()
    {
        Assert.Throws<ValidationException>(() => _paths.LearningFile("repo-b", "record.json"));
        Assert.Throws<ValidationException>(() => _paths.SessionFile("repo-a", "../other", "record.json"));
    }

    [Fact]
    public void RejectsExistingDirectoryLinkBeforeAnyWrite()
    {
        var target = Path.Combine(_root, "outside");
        var parent = Path.Combine(_root, "repositories", "repo-a", "AiLearning");
        var link = Path.Combine(parent, "temp");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(parent);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add("/c");
                start.ArgumentList.Add("mklink");
                start.ArgumentList.Add("/J");
                start.ArgumentList.Add(link);
                start.ArgumentList.Add(target);
                using var process = Process.Start(start)!;
                process.WaitForExit();
                Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
            }
            else Directory.CreateSymbolicLink(link, target);
            Assert.Throws<ValidationException>(() => _paths.LearningFile("repo-a", "temp", "record.json"));
            Assert.Empty(Directory.GetFiles(target));
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link); // unlink only; never recurse through the target
        }
    }

    public void Dispose()
    {
        _services.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
