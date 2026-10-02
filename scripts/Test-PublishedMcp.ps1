[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Executable,
    [Parameter(Mandatory)] [string] $SystemRoot,
    [ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')] [string] $RepositoryId = 'published-smoke',
    [ValidateSet('2025-11-25', '2026-07-28')] [string] $ProtocolVersion = '2025-11-25',
    [ValidateRange(5, 120)] [int] $TimeoutSeconds = 30,
    [string[]] $ArgumentList = @()
)

$ErrorActionPreference = 'Stop'
$binary = if (Test-Path -LiteralPath $Executable -PathType Leaf) {
    [IO.Path]::GetFullPath($Executable)
} else {
    (Get-Command $Executable -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
}
$root = [IO.Path]::GetFullPath($SystemRoot)
New-Item -ItemType Directory -Path $root -Force | Out-Null

$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $binary
$start.WorkingDirectory = Split-Path -Parent $binary
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.Environment['SystemStorage__Root'] = $root
$start.Environment['Repository__Id'] = $RepositoryId
$start.Environment['Embedding__TimeoutSeconds'] = '1'
$start.Environment['OTEL_SDK_DISABLED'] = 'true'
$start.Environment['Observability__ExporterEnabled'] = 'false'
foreach ($argument in $ArgumentList) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::new()
$process.StartInfo = $start
if (-not $process.Start()) { throw 'Published MCP process did not start.' }
$stderr = $process.StandardError.ReadToEndAsync()

function Send-JsonRpc([int] $Id, [string] $Method, [object] $Parameters) {
    if ($ProtocolVersion -eq '2026-07-28') {
        $Parameters['_meta'] = @{
            'io.modelcontextprotocol/protocolVersion' = $ProtocolVersion
            'io.modelcontextprotocol/clientCapabilities' = @{}
            'io.modelcontextprotocol/clientInfo' = @{ name = 'published-smoke'; version = '1.0' }
        }
    }
    $message = @{ jsonrpc = '2.0'; id = $Id; method = $Method; params = $Parameters }
    $process.StandardInput.WriteLine(($message | ConvertTo-Json -Depth 20 -Compress))
    $process.StandardInput.Flush()
}

function Read-JsonRpc([int] $Id) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $remaining = $deadline - [DateTimeOffset]::UtcNow
        $line = $process.StandardOutput.ReadLineAsync().WaitAsync($remaining).GetAwaiter().GetResult()
        if ($null -eq $line) {
            $details = $stderr.GetAwaiter().GetResult()
            throw "Published MCP exited before response $Id. stderr: $details"
        }
        try { $message = $line | ConvertFrom-Json -Depth 30 }
        catch { throw "Non-JSON content was written to MCP stdout: $line" }
        if ($message.jsonrpc -ne '2.0') { throw "Invalid JSON-RPC stdout line: $line" }
        if ($message.id -eq $Id) { return $message }
    }
    throw "Timed out waiting for JSON-RPC response $Id."
}

try {
    if ($ProtocolVersion -eq '2026-07-28') {
        Send-JsonRpc 1 'server/discover' @{}
        $discovery = Read-JsonRpc 1
        if ($discovery.error) { throw "Discovery failed: $($discovery | ConvertTo-Json -Depth 10 -Compress)" }
    } else {
        Send-JsonRpc 1 'initialize' @{
            protocolVersion = $ProtocolVersion
            capabilities = @{}
            clientInfo = @{ name = 'published-smoke'; version = '1.0' }
        }
        $initialize = Read-JsonRpc 1
        if ($initialize.error) { throw "Initialize failed: $($initialize | ConvertTo-Json -Depth 10 -Compress)" }
        if ($initialize.result.protocolVersion -ne $ProtocolVersion) { throw 'Server negotiated an unexpected protocol version.' }

        $process.StandardInput.WriteLine((@{
            jsonrpc = '2.0'
            method = 'notifications/initialized'
            params = @{}
        } | ConvertTo-Json -Depth 10 -Compress))
        $process.StandardInput.Flush()
    }

    Send-JsonRpc 2 'tools/list' @{}
    $listed = Read-JsonRpc 2
    if ($listed.error) { throw "tools/list failed: $($listed | ConvertTo-Json -Depth 10 -Compress)" }
    $names = @($listed.result.tools | ForEach-Object name)
    if ($names.Count -ne (@($names | Sort-Object -Unique)).Count) { throw 'Published server exposed duplicate tool names.' }
    $required = @(
        'append_agent_memory', 'resume_agent_session', 'coordinate_agent_task',
        'memory_status', 'memory_remember', 'memory_recall', 'memory_get',
        'memory_prepare_compaction', 'memory_commit_compaction', 'memory_reindex'
    )
    $missing = @($required | Where-Object { $_ -notin $names })
    if ($missing.Count) { throw "Published server is missing tools: $($missing -join ', ')" }

    Send-JsonRpc 3 'tools/call' @{
        name = 'create_or_activate_session'
        arguments = @{ request = @{
            sessionId = 'published-smoke-session'
            actorId = 'published-smoke-agent'
            operationId = "published-smoke-create-$($ProtocolVersion.Replace('-', ''))"
        } }
    }
    $successful = Read-JsonRpc 3
    if ($successful.error -or $successful.result.isError) { throw 'Published success-path tool call failed.' }

    Send-JsonRpc 4 'tools/call' @{
        name = 'memory_record_event'
        arguments = @{ request = @{
            eventId = 'published-smoke-invalid-event'
            eventType = 'memory-validated'
            agentId = 'published-smoke-agent'
        } }
    }
    $failed = Read-JsonRpc 4
    if ($failed.error -or -not $failed.result.isError) { throw 'Published validation-failure tool call did not return the expected MCP error result.' }

    $process.StandardInput.Close()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { throw 'Published MCP did not exit within the shutdown budget.' }
    $stderrText = $stderr.GetAwaiter().GetResult()
    $stderrLines = @($stderrText -split "`r?`n" | Where-Object { $_ })
    if ($stderrLines.Count -lt 2) { throw 'Expected bounded success and failure JSON diagnostics on stderr.' }
    foreach ($line in $stderrLines) {
        if ($line.Length -ge 4096) { throw 'Published stderr record exceeded the smoke bound.' }
        try { $null = $line | ConvertFrom-Json -Depth 20 }
        catch { throw "Non-JSON content was written to MCP stderr: $line" }
    }
    if (-not ($stderrLines -match 'mcp.tool.completed')) { throw 'Missing success diagnostic.' }
    if (-not ($stderrLines -match 'mcp.tool.failed')) { throw 'Missing failure diagnostic.' }
    Write-Output "Published MCP smoke passed: $($names.Count) unique tools; protocol $ProtocolVersion; success/failure calls; bounded JSON stderr; stdout JSON-RPC clean; graceful shutdown."
} finally {
    if (-not $process.HasExited) {
        $process.Kill($true)
        $process.WaitForExit(5000) | Out-Null
    }
    $process.Dispose()
}
