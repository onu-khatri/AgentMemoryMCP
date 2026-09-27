using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSession.MCP.Services;

namespace AgentSession.MCP.Tests;

internal sealed class McpProcess : IDisposable
{
    private readonly Process _process;
    private readonly Task<string> _stderr;
    private int _id;
    private readonly string _version;
    private static readonly JsonSerializerOptions Wire = new(MemoryJson.Options) { WriteIndented = false };
    public McpProcess(string root, string repository = "repo", string version = "2025-11-25", double leaseMinutes = 15)
    {
        _version = version;
        var folder = Path.Combine(AppContext.BaseDirectory, "worker");
        var start = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.Combine(folder, "AgentSession.MCP.TestWorker.runtimeconfig.json"),
            "--depsfile", Path.Combine(folder, "AgentSession.MCP.TestWorker.deps.json"), Path.Combine(folder, "AgentSession.MCP.dll") })
            start.ArgumentList.Add(argument);
        start.Environment["SystemStorage__Root"] = root;
        start.Environment["Repository__Id"] = repository;
        start.Environment["Embedding__TimeoutSeconds"] = "1";
        start.Environment["SessionCoordination__ClaimLeaseMinutes"] = leaseMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _process = Process.Start(start)!;
        _stderr = _process.StandardError.ReadToEndAsync();
    }
    public async Task InitializeAsync()
    {
        if (_version == "2026-07-28")
        {
            var discovered = await RequestAsync("server/discover", new { });
            Assert.False(discovered.TryGetProperty("error", out _), discovered.ToString());
            return;
        }
        var result = await RequestAsync("initialize", new { protocolVersion = _version, capabilities = new { },
            clientInfo = new { name = "agent-memory-tests", version = "1.0" } });
        Assert.Equal(_version, result.GetProperty("result").GetProperty("protocolVersion").GetString());
        await NotifyAsync("notifications/initialized", new { });
    }
    public async Task<int> SendAsync(string method, object parameters)
    {
        var id = ++_id;
        var node = JsonSerializer.SerializeToNode(parameters, Wire)!.AsObject();
        if (_version == "2026-07-28") node["_meta"] = new JsonObject { ["io.modelcontextprotocol/protocolVersion"] = _version,
            ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject(),
            ["io.modelcontextprotocol/clientInfo"] = new JsonObject { ["name"] = "agent-memory-tests", ["version"] = "1.0" } };
        await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = node }, Wire));
        return id;
    }
    public async Task<JsonElement> ReadAsync(int id)
    {
        while (true)
        {
            var line = await _process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
            if (line is null) throw new InvalidOperationException("MCP exited: " + await _stderr);
            // Every stdout line must be valid JSON-RPC, never a host log.
            using var json = JsonDocument.Parse(line);
            Assert.Equal("2.0", json.RootElement.GetProperty("jsonrpc").GetString());
            if (json.RootElement.TryGetProperty("id", out var responseId) && responseId.ValueKind == JsonValueKind.Number && responseId.GetInt32() == id)
                return json.RootElement.Clone();
        }
    }
    public async Task<JsonElement> RequestAsync(string method, object parameters) => await ReadAsync(await SendAsync(method, parameters));
    public Task NotifyAsync(string method, object parameters)
        => _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", method, @params = parameters }, Wire));
    public async Task<JsonElement> CallAsync(string name, object request)
    {
        var response = await RequestAsync("tools/call", new { name, arguments = new { request } });
        Assert.False(response.TryGetProperty("error", out _), response.ToString());
        var result = response.GetProperty("result");
        Assert.False(result.TryGetProperty("isError", out var error) && error.GetBoolean(), result.ToString());
        return result.GetProperty("structuredContent").Clone();
    }
    public async Task<JsonElement> CallWithoutRequestAsync(string name)
    {
        var response = await RequestAsync("tools/call", new { name, arguments = new { } });
        Assert.False(response.TryGetProperty("error", out _), response.ToString());
        var result = response.GetProperty("result");
        Assert.False(result.TryGetProperty("isError", out var error) && error.GetBoolean(), result.ToString());
        return result.GetProperty("structuredContent").Clone();
    }
    public void Dispose()
    {
        if (!_process.HasExited) { _process.Kill(entireProcessTree: true); _process.WaitForExit(5000); }
        _process.Dispose();
    }
}
