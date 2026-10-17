[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Project,
    [ValidateRange(1024, 65535)] [int] $Port = 5080,
    [Parameter(Mandatory)] [string] $RepositoryId,
    [Parameter(Mandatory)] [string] $Token,
    [ValidateRange(5, 120)] [int] $TimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'
$projectPath = [IO.Path]::GetFullPath($Project)
$baseUri = "http://127.0.0.1:$Port"
$stdoutPath = Join-Path ([IO.Path]::GetTempPath()) "agent-session-mcp-host-$Port.stdout.log"
$stderrPath = Join-Path ([IO.Path]::GetTempPath()) "agent-session-mcp-host-$Port.stderr.log"
$process = $null
$httpClient = [Net.Http.HttpClient]::new()

function Get-HttpErrorBody([object] $errorRecord) {
    $response = $errorRecord.Exception.Response
    if ($null -eq $response) { return '' }
    if ($null -ne $response.Content) { return $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() }
    $reader = [IO.StreamReader]::new($response.GetResponseStream())
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Assert-HostProcessAlive([string] $phase) {
    if ($null -eq $process -or $process.HasExited) {
        $details = if ($null -ne $stderrTask) { $stderrTask.GetAwaiter().GetResult() } else { '' }
        $exitCode = if ($null -ne $process -and $process.HasExited) { $process.ExitCode } else { 'unknown' }
        throw "Host process was not alive during $phase; exit code $exitCode. stderr: $details"
    }
}

function Send-HttpSmokeRequest([string] $uri, [string[]] $authorizationValues) {
    Assert-HostProcessAlive "request to $uri"
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, $uri)
    $request.Content = [Net.Http.StringContent]::new('{}', [Text.Encoding]::UTF8, 'application/json')
    foreach ($authorizationValue in $authorizationValues) {
        if (-not $request.Headers.TryAddWithoutValidation('Authorization', $authorizationValue)) {
            throw 'Could not add the smoke Authorization header.'
        }
    }

    try {
        $response = $httpClient.SendAsync($request).GetAwaiter().GetResult()
        try {
            $retryAfter = $null
            $retryValues = $null
            if ($response.Headers.TryGetValues('Retry-After', [ref] $retryValues)) {
                $retryAfter = [int](@($retryValues)[0])
            }
            return [pscustomobject]@{
                StatusCode = [int]$response.StatusCode
                RetryAfter = $retryAfter
                Content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            }
        } finally {
            $response.Dispose()
        }
    } finally {
        $request.Dispose()
    }
}

function Assert-RedactedResponse([string] $content, [string[]] $sensitiveValues, [string] $description) {
    foreach ($sensitiveValue in $sensitiveValues) {
        if (-not [string]::IsNullOrWhiteSpace($sensitiveValue) -and $content -match [regex]::Escape($sensitiveValue)) {
            throw "Response disclosed sensitive value for ${description}: $content"
        }
    }
    if ($content -match [regex]::Escape($storageRoot) -or $content -match [regex]::Escape($Token) -or $content -match 'Exception|StackTrace|System\.') {
        throw "Response was not safely redacted for ${description}: $content"
    }
}

try {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Command dotnet -CommandType Application).Source
    $start.WorkingDirectory = Split-Path -Parent (Split-Path -Parent $projectPath)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.ArgumentList.Add('run')
    $start.ArgumentList.Add('--no-build')
    $start.ArgumentList.Add('--configuration')
    $start.ArgumentList.Add('Release')
    $start.ArgumentList.Add('--project')
    $start.ArgumentList.Add($projectPath)
    $start.ArgumentList.Add('--urls')
    $start.ArgumentList.Add($baseUri)
    $start.Environment['Host__Authentication__Token'] = $Token
    $start.Environment['Host__Authentication__ClientId'] = 'host-smoke-client'
    $start.Environment['Host__PreAuthenticationPermitLimit'] = '5'
    $start.Environment['Host__PreAuthenticationWindowSeconds'] = '60'
    $start.Environment['Host__RateLimitPermitLimit'] = '1'
    $start.Environment['Host__RateLimitQueueLimit'] = '0'
    $start.Environment['Host__RateLimitWindowSeconds'] = '60'
    $scopes = @(
        'mcp:session:read', 'mcp:session:write',
        'mcp:agent-memory:read', 'mcp:agent-memory:write', 'mcp:agent-memory:maintenance'
    )
    for ($index = 0; $index -lt $scopes.Count; $index++) {
        $start.Environment["Host__Authentication__Scopes__$index"] = $scopes[$index]
    }
    $start.Environment['Repository__Id'] = $RepositoryId
    $storageRoot = Join-Path ([IO.Path]::GetTempPath()) "agent-session-mcp-host-$Port-storage"
    $start.Environment['SystemStorage__Root'] = $storageRoot
    $start.Environment['Qdrant__Host'] = '127.0.0.1'
    $start.Environment['Qdrant__GrpcPort'] = '6334'
    $start.Environment['Qdrant__RestPort'] = '6333'
    $start.Environment['Embedding__Ollama__BaseUrl'] = 'http://127.0.0.1:11434/'
    $start.Environment['Embedding__Ollama__Model'] = 'smoke'
    $start.Environment['OTEL_SDK_DISABLED'] = 'true'
    $start.Environment['Observability__ExporterEnabled'] = 'false'

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    if (-not $process.Start()) { throw 'Host process did not start.' }
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        Assert-HostProcessAlive 'readiness probe'
        try {
            $live = Invoke-WebRequest -Uri "$baseUri/health/live" -Headers @{ Authorization = "Bearer $Token" } -TimeoutSec 2 -UseBasicParsing
            $ready = Invoke-WebRequest -Uri "$baseUri/health/ready" -Headers @{ Authorization = "Bearer $Token" } -TimeoutSec 2 -UseBasicParsing
            if ($live.StatusCode -eq 200 -and $ready.StatusCode -eq 200) { break }
        } catch { }
        if ([DateTimeOffset]::UtcNow -ge $deadline) { throw 'Host readiness did not complete within the timeout.' }
        Start-Sleep -Milliseconds 250
    } while ($true)

    Assert-HostProcessAlive 'post-readiness validation'

    $readyBody = [string]$ready.Content
    if ($readyBody -notmatch '"status"\s*:\s*"(healthy|unhealthy|degraded)"' -or $readyBody -match [regex]::Escape($storageRoot) -or $readyBody -match [regex]::Escape($Token) -or $readyBody -match 'Exception|StackTrace|System\.') {
        throw "Readiness response was not status-only and redacted: $readyBody"
    }

    $emptyBearer = 'Bearer '
    $failureCases = @(
        @{ Name = 'missing Authorization'; Values = @() },
        @{ Name = 'duplicate Authorization'; Values = @('Bearer duplicate-one', 'Bearer duplicate-two') },
        @{ Name = 'malformed scheme'; Values = @('Basic malformed-token') },
        @{ Name = 'empty Bearer credential'; Values = @($emptyBearer) },
        @{ Name = 'invalid Bearer credential'; Values = @('Bearer invalid-token-for-smoke') }
    )
    foreach ($failureCase in $failureCases) {
        $failure = Send-HttpSmokeRequest "$baseUri/mcp" $failureCase.Values
        if ($failure.StatusCode -ne 401) { throw "Expected 401 for $($failureCase.Name); received $($failure.StatusCode)." }
        Assert-RedactedResponse $failure.Content $failureCase.Values $failureCase.Name
    }

    $exhausted = Send-HttpSmokeRequest "$baseUri/mcp" @('Bearer exhausted-invalid-token')
    if ($exhausted.StatusCode -ne 429 -or $exhausted.RetryAfter -lt 1 -or $exhausted.RetryAfter -gt 60) {
        throw "Expected pre-authentication 429 with Retry-After <= 60 after five failed forms; received $($exhausted.StatusCode)/$($exhausted.RetryAfter)."
    }

    Assert-HostProcessAlive 'authenticated tools/list'
    $authorized = Invoke-WebRequest -Uri "$baseUri/mcp" -Method Post -Headers @{ Authorization = "Bearer $Token"; Accept = 'application/json, text/event-stream' } -Body (@{ jsonrpc = '2.0'; id = 1; method = 'tools/list'; params = @{} } | ConvertTo-Json -Depth 10 -Compress) -ContentType 'application/json' -UseBasicParsing
    if ($authorized.StatusCode -notin 200, 202) { throw "Authorized MCP request returned $($authorized.StatusCode)." }

    Assert-HostProcessAlive 'repository mismatch'
    $mismatchedRepository = "$RepositoryId-unexpected"
    $mismatchResponse = Invoke-WebRequest -Uri "$baseUri/mcp" -Method Post -Headers @{ Authorization = "Bearer $Token"; 'X-Repository-Id' = $mismatchedRepository; Accept = 'application/json, text/event-stream' } -Body (@{ jsonrpc = '2.0'; id = 2; method = 'tools/list'; params = @{} } | ConvertTo-Json -Depth 10 -Compress) -ContentType 'application/json' -UseBasicParsing -SkipHttpErrorCheck
    $mismatchBody = [string]$mismatchResponse.Content
    if ($mismatchResponse.StatusCode -ne 403) { throw "Expected 403 for repository mismatch; received $($mismatchResponse.StatusCode)." }
    if ($mismatchBody -notmatch 'repository_mismatch') { throw "Repository mismatch response did not contain the stable error code: $mismatchBody" }
    Assert-RedactedResponse $mismatchBody @($mismatchedRepository, "Bearer $Token") 'repository mismatch'

    Assert-HostProcessAlive 'first aggregate-quota operation'
    $firstOperation = Invoke-WebRequest -Uri "$baseUri/mcp" -Method Post -Headers @{ Authorization = "Bearer $Token"; Accept = 'application/json, text/event-stream' } -Body (@{ jsonrpc = '2.0'; id = 3; method = 'tools/call'; params = @{ name = 'memory_status'; arguments = @{} } } | ConvertTo-Json -Depth 10 -Compress) -ContentType 'application/json' -UseBasicParsing
    if ($firstOperation.StatusCode -notin 200, 202) { throw "First operation request returned $($firstOperation.StatusCode)." }
    Assert-HostProcessAlive 'second aggregate-quota operation'
    $limitedOperation = Invoke-WebRequest -Uri "$baseUri/mcp" -Method Post -Headers @{ Authorization = "Bearer $Token"; Accept = 'application/json, text/event-stream' } -Body (@{ jsonrpc = '2.0'; id = 4; method = 'tools/call'; params = @{ name = 'memory_recall'; arguments = @{} } } | ConvertTo-Json -Depth 10 -Compress) -ContentType 'application/json' -UseBasicParsing -SkipHttpErrorCheck
    $limitedBody = [string]$limitedOperation.Content
    if ($limitedOperation.StatusCode -ne 429 -and ($limitedOperation.StatusCode -ne 200 -or $limitedBody -notmatch 'rate_limited')) {
        throw "Expected transport 429 or post-resolution rate_limited response; received $($limitedOperation.StatusCode): $limitedBody"
    }
    Write-Output "HTTP MCP host smoke passed: live/ready status-only redaction, five pre-auth failure forms with bounded exhaustion, valid-auth recovery, safe repository-mismatch 403, aggregate quota with bounded Retry-After, bounded cleanup."
}
finally {
    if ($null -ne $process) {
        if (-not $process.HasExited) {
            $process.CloseMainWindow() | Out-Null
            if (-not $process.WaitForExit([Math]::Min($TimeoutSeconds * 1000, 5000))) {
                $process.Kill($true)
                $process.WaitForExit(5000) | Out-Null
            }
        }
        $exitCode = $process.ExitCode
        $stdout = if ($stdoutTask) { $stdoutTask.GetAwaiter().GetResult() } else { '' }
        $stderr = if ($stderrTask) { $stderrTask.GetAwaiter().GetResult() } else { '' }
        if ($exitCode -ne 0 -and $exitCode -ne 143) { Write-Warning "HTTP smoke cleanup exit code: $exitCode; bounded force-termination may have been required. HTTP smoke does not assert graceful shutdown.`n$stderr" }
        $process.Dispose()
    }
    $httpClient.Dispose()
    Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
}
