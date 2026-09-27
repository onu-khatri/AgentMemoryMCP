[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Executable,
    [Parameter(Mandatory)] [string] $SystemRoot,
    [ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')] [string] $RepositoryId = 'published-smoke',
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
foreach ($argument in $ArgumentList) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::new()
$process.StartInfo = $start
if (-not $process.Start()) { throw 'Published MCP process did not start.' }
$stderr = $process.StandardError.ReadToEndAsync()

function Send-JsonRpc([int] $Id, [string] $Method, [object] $Parameters) {
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
    Send-JsonRpc 1 'initialize' @{
        protocolVersion = '2025-11-25'
        capabilities = @{}
        clientInfo = @{ name = 'published-smoke'; version = '1.0' }
    }
    $initialize = Read-JsonRpc 1
    if ($initialize.error) { throw "Initialize failed: $($initialize | ConvertTo-Json -Depth 10 -Compress)" }
    if ($initialize.result.protocolVersion -ne '2025-11-25') { throw 'Server negotiated an unexpected protocol version.' }

    $process.StandardInput.WriteLine((@{
        jsonrpc = '2.0'
        method = 'notifications/initialized'
        params = @{}
    } | ConvertTo-Json -Depth 10 -Compress))
    $process.StandardInput.Flush()

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
    Write-Output "Published MCP smoke passed: $($names.Count) unique tools; protocol 2025-11-25; stdout JSON-RPC clean."
} finally {
    if (-not $process.HasExited) {
        $process.Kill($true)
        $process.WaitForExit(5000) | Out-Null
    }
    $process.Dispose()
}
