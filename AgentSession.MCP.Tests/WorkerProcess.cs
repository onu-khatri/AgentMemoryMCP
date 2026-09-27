using System.Diagnostics;

namespace AgentSession.MCP.Tests;

internal sealed class WorkerProcess : IDisposable
{
    public Process Process { get; }
    public WorkerProcess(string command, string root)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "worker", "AgentSession.MCP.TestWorker.dll"));
        start.ArgumentList.Add(command);
        start.ArgumentList.Add(root);
        Process = Process.Start(start)!;
    }
    public async Task<string?> ReadLineAsync() => await Process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
    public async Task<int> ExitAsync()
    {
        await Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        return Process.ExitCode;
    }
    public void Dispose()
    {
        if (!Process.HasExited) { Process.Kill(entireProcessTree: true); Process.WaitForExit(5000); }
        Process.Dispose();
    }
}
