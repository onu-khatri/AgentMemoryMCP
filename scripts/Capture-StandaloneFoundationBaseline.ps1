[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [string] $EvidenceRoot = 'openspec/changes/standalone-mcp-modular-solution-foundation/evidence/baselines',
    [switch] $SkipBuildDependentChecks
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidencePath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $EvidenceRoot))
$pathComparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }

function Assert-ContainedPath([string] $Path, [string] $Root, [string] $Label) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($fullRoot, $pathComparison)) {
        throw "$Label must remain under repository root: $fullPath"
    }
    return $fullPath
}

$evidencePath = Assert-ContainedPath $evidencePath $repositoryRoot 'EvidenceRoot'
New-Item -ItemType Directory -Path $evidencePath -Force | Out-Null
$centralPackageVersions = @{}
$centralPackagesPath = Join-Path $repositoryRoot 'Directory.Packages.props'
if (Test-Path -LiteralPath $centralPackagesPath) {
    [xml]$centralPackages = Get-Content -LiteralPath $centralPackagesPath -Raw
    foreach ($packageVersion in @($centralPackages.Project.ItemGroup.PackageVersion) | Where-Object { $_ }) {
        $centralPackageVersions[[string]$packageVersion.Include] = [string]$packageVersion.Version
    }
}

function ConvertTo-StableJson([object] $Value) {
    return $Value | ConvertTo-Json -Depth 100
}

function Write-JsonFile([string] $Path, [object] $Value) {
    $fullPath = Assert-ContainedPath $Path $repositoryRoot 'Evidence file'
    $parent = Split-Path -Parent $fullPath
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    [IO.File]::WriteAllText($fullPath, (ConvertTo-StableJson $Value) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
}

function Get-RelativePath([string] $Path) {
    return [IO.Path]::GetRelativePath($repositoryRoot, [IO.Path]::GetFullPath($Path)).Replace('\', '/')
}

function Protect-Diagnostic([string] $Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return '' }
    $sanitized = $Text.Replace($repositoryRoot, '${REPOSITORY_ROOT}', $pathComparison)
    $userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    if (-not [string]::IsNullOrWhiteSpace($userProfile)) {
        $sanitized = $sanitized.Replace($userProfile, '${USER_PROFILE}', $pathComparison)
    }
    if ($sanitized.Length -gt 8000) { $sanitized = $sanitized.Substring(0, 8000) + '...<truncated>' }
    return $sanitized.Trim()
}

$results = [Collections.Generic.List[object]]::new()
function Add-Result(
    [string] $Name,
    [ValidateSet('passed', 'failed-pre-existing', 'failed-regression', 'skipped', 'unverified')] [string] $Status,
    [string] $Command,
    [int] $ExitCode,
    [string] $Diagnostic,
    [string] $Prerequisite = ''
) {
    $results.Add([ordered]@{
        name = $Name
        status = $Status
        command = Protect-Diagnostic $Command
        utc = [DateTimeOffset]::UtcNow.ToString('O')
        exitCode = $ExitCode
        diagnostic = Protect-Diagnostic $Diagnostic
        prerequisite = $Prerequisite
    })
}

function Invoke-Recorded([string] $Name, [string] $FileName, [string[]] $Arguments) {
    $display = $FileName + ' ' + ($Arguments -join ' ')
    $output = & $FileName @Arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    $status = if ($exitCode -eq 0) { 'passed' } else { 'failed-regression' }
    Add-Result $Name $status $display $exitCode $output
    return [pscustomobject]@{ Name = $Name; Status = $status; ExitCode = $exitCode; Output = $output }
}

function Get-ProjectData([string] $ProjectPath) {
    [xml]$project = Get-Content -LiteralPath $ProjectPath -Raw
    $packages = @(
        $project.Project.ItemGroup.PackageReference |
            Where-Object { $_ } |
            ForEach-Object {
                $version = [string]$_.Version
                if ([string]::IsNullOrWhiteSpace($version) -and $centralPackageVersions.ContainsKey([string]$_.Include)) {
                    $version = $centralPackageVersions[[string]$_.Include]
                }
                [ordered]@{
                    id = [string]$_.Include
                    version = $version
                }
            } |
            Sort-Object id
    )
    $references = @(
        $project.Project.ItemGroup.ProjectReference |
            Where-Object { $_ } |
            ForEach-Object {
                $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $ProjectPath) ([string]$_.Include)))
                Get-RelativePath $resolved
            } |
            Sort-Object
    )
    return [ordered]@{
        path = Get-RelativePath $ProjectPath
        projectReferences = $references
        packageReferences = $packages
    }
}

function Get-Ownership([string] $RelativePath) {
    $owner = $null
    $reason = ''
    $rules = @()
    $disposition = 'move-later'
    $laterTask = ''
    if ($RelativePath -match '^src/AgentSession\.MCP\.Core/') { $owner = 'Core'; $reason = 'Platform-neutral request, error, execution and storage contracts'; $rules = @(6, 10); $laterTask = '3.x-4.x' }
    elseif ($RelativePath -match '^src/AgentSession\.MCP\.Protocol/') { $owner = 'Protocol'; $reason = 'MCP protocol adapters, filters, mapping and public capability metadata'; $rules = @(5, 12); $laterTask = '7.x' }
    elseif ($RelativePath -match '^src/AgentSession\.MCP\.Host/') { $owner = 'Host'; $reason = 'HTTP transport, authentication and operational composition'; $rules = @(4, 9); $laterTask = '8.2-8.5' }
    elseif ($RelativePath -match '^src/AgentSession\.MCP\.Infrastructure/') { $owner = 'Infrastructure'; $reason = 'Repository-scoped persistence and dependency adapters'; $rules = @(9, 14); $laterTask = '4.x' }
    elseif ($RelativePath -match '^src/AgentSession\.MCP\.ServiceDefaults/') { $owner = 'ServiceDefaults'; $reason = 'Independent observability, health and resilience defaults'; $rules = @(4, 9); $laterTask = '8.1' }
    elseif ($RelativePath -match '^src/AgentSession\.MCP\.StdioHost/') { $owner = 'StdioHost'; $reason = 'Stdio composition, packaging and process lifecycle'; $rules = @(4, 13); $laterTask = '8.4' }
    elseif ($RelativePath -match '^src/Modules/Session/AgentSession\.MCP\.Session\.Contracts/') { $owner = 'Session.Contracts'; $reason = 'Session public contracts and registration metadata'; $rules = @(7); $laterTask = '3.3,7.x' }
    elseif ($RelativePath -match '^src/Modules/Session/AgentSession\.MCP\.Session/') { $owner = 'Session'; $reason = 'Session module behavior and composition'; $rules = @(7, 14); $laterTask = '5.x' }
    elseif ($RelativePath -match '^src/Modules/AgentMemory/AgentSession\.MCP\.AgentMemory\.Contracts/') { $owner = 'AgentMemory.Contracts'; $reason = 'AgentMemory public contracts and registration metadata'; $rules = @(7); $laterTask = '3.4,7.x' }
    elseif ($RelativePath -match '^src/Modules/AgentMemory/AgentSession\.MCP\.AgentMemory/') { $owner = 'AgentMemory'; $reason = 'AgentMemory behavior and provider integration'; $rules = @(7, 9, 14); $laterTask = '6.x' }
    if ($owner) {
        $knownLocalDependencies = @()
        if ($RelativePath.EndsWith('.cs', [StringComparison]::OrdinalIgnoreCase)) {
            $sourcePath = Join-Path $repositoryRoot $RelativePath
            $knownLocalDependencies = @(Select-String -LiteralPath $sourcePath -Pattern '^using\s+(AgentSession\.MCP(?:\.[A-Za-z0-9_.]+)?)\s*;' | ForEach-Object { $_.Matches[0].Groups[1].Value } | Sort-Object -Unique)
        }
        return [pscustomobject][ordered]@{ path = $RelativePath; targetOwner = $owner; reason = $reason; ruleIds = $rules; knownLocalDependencies = $knownLocalDependencies; disposition = 'remain'; laterOwningTask = $laterTask }
    }
    switch -Regex ($RelativePath) {
        '^AgentSession\.MCP/Compatibility/ContractTypeForwarders\.cs$' {
            $owner = 'StdioHost'; $reason = 'Legacy stdio compatibility forwarding'; $rules = @(4, 13); $laterTask = '8.4'; $disposition = 'remain'; break
        }
        '^AgentSession\.MCP/(Program\.cs|Properties/AssemblyInfo\.cs|Services/EofSignalingReadStream\.cs|\.mcp/server\.json|README\.md|AgentSession\.MCP\.csproj)$' {
            $owner = 'StdioHost'; $reason = 'Legacy stdio composition, packaging, and process lifecycle'; $rules = @(4, 13); $laterTask = '8.4'; if ($RelativePath -notmatch '\.cs$') { $disposition = 'remain' }; break
        }
        '^AgentSession\.MCP/Tools/' {
            $owner = 'Protocol'; $reason = 'MCP tool adapters and protocol schemas'; $rules = @(5, 12); $laterTask = '7.x'; break
        }
        '^AgentSession\.MCP/Observability/AgentMemoryMcpTelemetryOutcomeClassifier\.cs$' {
            $owner = 'Protocol'; $reason = 'MCP outcome classification adapter'; $rules = @(5, 16); $laterTask = '7.x'; $disposition = 'split-later'; break
        }
        '^AgentSession\.MCP/Helpers/ValidationException\.cs$' {
            $owner = 'Core'; $reason = 'Candidate platform-neutral validation error seam'; $rules = @(6, 10); $laterTask = '3.2'; break
        }
        '^AgentSession\.MCP/Contracts/Requests/(CoordinateTaskRequest|SessionCoordinationRequests|SessionResumeRequests)\.cs$' {
            $owner = 'Session.Contracts'; $reason = 'Session lifecycle, coordination, and resume contracts'; $rules = @(7); $laterTask = '3.3'; break
        }
        '^AgentSession\.MCP/Models/(CoordinatedTask|SessionCoordination|SessionCoordinationMetadata|SessionWorkingContext|SharedSessionArtifact)\.cs$' {
            $owner = 'Session.Contracts'; $reason = 'Session-owned public model'; $rules = @(7); $laterTask = '3.3'; break
        }
        '^AgentSession\.MCP/Contracts/' {
            $owner = 'AgentMemory.Contracts'; $reason = 'Reusable memory request or response contract'; $rules = @(7); $laterTask = '3.4'; break
        }
        '^AgentSession\.MCP/Models/Memory/(CompactionDetails|MemoryEvent|MemoryOutcome|MemoryProvenance|MemoryRecord)\.cs$' {
            $owner = 'AgentMemory.Contracts'; $reason = 'Reusable memory public model'; $rules = @(7); $laterTask = '3.4'; break
        }
        '^AgentSession\.MCP/(Options/SessionCoordinationOptions|Services/(SessionCoordinationService|SessionResumeService|SharedSessionLifecycleService))\.cs$' {
            $owner = 'Session'; $reason = 'Session lifecycle, coordination, resume, or checkpoint behavior'; $rules = @(7, 14); $laterTask = '5.x'; break
        }
        '^AgentSession\.MCP/Interfaces/IMemoryContentPolicy\.cs$' {
            $owner = 'AgentMemory'; $reason = 'AgentMemory-specific policy contract'; $rules = @(7, 9); $laterTask = '6.x'; break
        }
        '^AgentSession\.MCP/(Options/MemoryPolicyOptions|Models/Memory/(EmbeddingMetadata|VectorMigration|VectorPendingWork)|Services/(CanonicalMemoryService.*|EmbeddingProjection|LearningCatalog|MemoryClaimHash|MemoryContentPolicy|MemoryMaintenancePass|MemoryMaintenanceService|MemoryRecordValidator|MemoryReindexService|MemoryRequestValidator|MemoryStatusService|OllamaEmbeddingClient|OperatorGrantVerifier|QdrantVectorIndex|VectorIndexCoordinator|VectorMigrationService))\.cs$' {
            $owner = 'AgentMemory'; $reason = 'Reusable memory behavior or module-specific provider integration'; $rules = @(7, 9, 14); $laterTask = '6.x'; break
        }
        '^AgentSession\.MCP/(Helpers/NameSanitizer|Options/(SystemStorageOptions|RepositoryOptions)|Services/(MemoryStoragePaths|ManagedStoragePathResolver|ManagedTransactionStore|RepositoryMutationLock|SystemFileSystem|MemoryJson)|Observability/(McpDependencyTelemetry|NoOpMcpDependencyFailureRecorder))\.cs$' {
            $owner = 'Infrastructure'; $reason = 'Cross-module filesystem, transaction, locking, serialization, or dependency telemetry primitive'; $rules = @(9, 14); $laterTask = '4.x'; $disposition = 'split-later'; break
        }
        '^AgentSession\.MCP/(Extensions/|Observability/)' {
            $owner = 'Host'; $reason = 'Current mixed composition and observability registration'; $rules = @(4, 9); $laterTask = '3-8'; $disposition = 'split-later'; break
        }
    }
    if ($null -eq $owner) { return $null }
    $knownLocalDependencies = @()
    if ($RelativePath.EndsWith('.cs', [StringComparison]::OrdinalIgnoreCase)) {
        $sourcePath = Join-Path $repositoryRoot $RelativePath
        $knownLocalDependencies = @(
            Select-String -LiteralPath $sourcePath -Pattern '^using\s+(AgentSession\.MCP(?:\.[A-Za-z0-9_.]+)?)\s*;' |
                ForEach-Object { $_.Matches[0].Groups[1].Value } |
                Sort-Object -Unique
        )
    }
    return [pscustomobject][ordered]@{
        path = $RelativePath
        targetOwner = $owner
        reason = $reason
        ruleIds = $rules
        knownLocalDependencies = $knownLocalDependencies
        disposition = $disposition
        laterOwningTask = $laterTask
    }
}

function Write-BuildIndependentEvidence {
    $workerProject = if (Test-Path -LiteralPath (Join-Path $repositoryRoot 'tests/AgentSession.MCP.TestWorker/AgentSession.MCP.TestWorker.csproj')) {
        'tests/AgentSession.MCP.TestWorker/AgentSession.MCP.TestWorker.csproj'
    } else {
        'AgentSession.MCP.TestWorker/AgentSession.MCP.TestWorker.csproj'
    }
    $benchmarkProject = if (Test-Path -LiteralPath (Join-Path $repositoryRoot 'benchmarks/AgentSession.MCP.Benchmarks/AgentSession.MCP.Benchmarks.csproj')) {
        'benchmarks/AgentSession.MCP.Benchmarks/AgentSession.MCP.Benchmarks.csproj'
    } else {
        'AgentSession.MCP.Benchmarks/AgentSession.MCP.Benchmarks.csproj'
    }
    $projectPaths = @(
        Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src'), (Join-Path $repositoryRoot 'tests'), (Join-Path $repositoryRoot 'benchmarks') -Recurse -Filter '*.csproj' -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj|Fixtures)[\\/]' } |
            Select-Object -ExpandProperty FullName
    ) | Sort-Object
    $projectGraph = @($projectPaths | ForEach-Object { Get-ProjectData $_ })
    Write-JsonFile (Join-Path $evidencePath 'project-package-graph.json') ([ordered]@{
        capturedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        projects = $projectGraph
    })

    $stdioProjectPath = Join-Path $repositoryRoot 'src/AgentSession.MCP.StdioHost/AgentSession.MCP.StdioHost.csproj'
    [xml]$stdioProject = Get-Content -LiteralPath $stdioProjectPath -Raw
    $serverManifest = Get-Content -LiteralPath (Join-Path $repositoryRoot 'src/AgentSession.MCP.StdioHost/.mcp/server.json') -Raw | ConvertFrom-Json -Depth 50
    $directPackages = @(
        $projectGraph |
            ForEach-Object { @($_.packageReferences) } |
            ForEach-Object { $_ } |
            Where-Object id |
            ForEach-Object {
                [pscustomobject][ordered]@{
                    id = [string]$_['id']
                    version = [string]$_['version']
                }
            } |
            Sort-Object -Property id, version -Unique
    )
    Write-JsonFile (Join-Path $evidencePath 'package-and-command.json') ([ordered]@{
        packageId = [string]$stdioProject.Project.PropertyGroup.PackageId
        packageVersion = [string]$stdioProject.Project.PropertyGroup.PackageVersion
        packageType = [string]$stdioProject.Project.PropertyGroup.PackageType
        packAsTool = [string]$stdioProject.Project.PropertyGroup.PackAsTool
        runtimeIdentifiers = ([string]$stdioProject.Project.PropertyGroup.RuntimeIdentifiers -split ';')
        commandMetadata = $serverManifest
        directPackages = $directPackages
        packageInspection = [ordered]@{ status = 'unverified'; files = @(); nuspec = $null }
    })

    $inventory = @(
        Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Recurse -File |
            Where-Object {
                $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
                (
                $_.Extension -eq '.cs' -or
                $_.Extension -eq '.csproj' -or
                $_.Name -eq 'README.md' -or
                $_.FullName.EndsWith('src\AgentSession.MCP.StdioHost\.mcp\server.json', $pathComparison)
                )
            } |
            ForEach-Object { Get-RelativePath $_.FullName } |
            Sort-Object -Unique
    )
    $ownership = @($inventory | ForEach-Object { Get-Ownership $_ })
    $unassigned = @($inventory | Where-Object { -not (Get-Ownership $_) })
    $duplicates = @($ownership | Where-Object { $_ } | Group-Object path | Where-Object Count -ne 1)
    Write-JsonFile (Join-Path $evidencePath 'source-ownership.json') ([ordered]@{
        capturedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        expectedCount = $inventory.Count
        assignedCount = @($ownership | Where-Object { $_ }).Count
        unassigned = $unassigned
        duplicates = @($duplicates.Name)
        entries = @($ownership | Where-Object { $_ } | Sort-Object path)
    })
    if ($unassigned.Count -or $duplicates.Count) {
        throw "Ownership inventory failed. Unassigned: $($unassigned -join ', '); duplicates: $($duplicates.Name -join ', ')"
    }

    $seams = @(
        [ordered]@{ source = 'Protocol tools'; target = 'Session and AgentMemory services'; reason = 'Tool adapters directly invoke concrete implementations'; laterTask = '3.x,5.x,6.x,7.x' },
        [ordered]@{ source = 'MemoryConfigurationExtensions'; target = 'Session, AgentMemory, Infrastructure, providers, telemetry'; reason = 'Monolithic registration crosses all future owners'; laterTask = '3.x-8.x' },
        [ordered]@{ source = 'Public contracts'; target = 'Mixed implementation models'; reason = 'Current project does not enforce contracts-to-implementation direction'; laterTask = '3.3,3.4' },
        [ordered]@{ source = 'MCP telemetry classifier'; target = 'Domain validation and MCP outcomes'; reason = 'Classifier spans protocol and domain normalization'; laterTask = '3.2,7.x' },
        [ordered]@{ source = 'TestWorker and Benchmarks'; target = 'Legacy monolith'; reason = 'Temporary compatibility references'; laterTask = '9.6' }
    )
    Write-JsonFile (Join-Path $evidencePath 'cross-owner-seams.json') ([ordered]@{
        capturedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        seams = $seams
        unassigned = 0
    })
    Add-Result 'source-project-package-inventory' 'passed' 'repository inspection' 0 "Captured $($inventory.Count) production assets, $($projectGraph.Count) projects, and $($directPackages.Count) direct package entries."
}

function New-McpProcess([string] $Executable, [string] $StorageRoot, [string] $RepositoryId, [string] $ProtocolVersion) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.WorkingDirectory = Split-Path -Parent $Executable
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment['SystemStorage__Root'] = $StorageRoot
    $start.Environment['Repository__Id'] = $RepositoryId
    $start.Environment['Embedding__TimeoutSeconds'] = '1'
    $start.Environment['Qdrant__TimeoutSeconds'] = '1'
    $start.Environment['OTEL_SDK_DISABLED'] = 'true'
    $start.Environment['Observability__ExporterEnabled'] = 'false'
    $start.Environment['Observability__ShutdownFlushTimeoutMilliseconds'] = '1000'
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    if (-not $process.Start()) { throw 'MCP process did not start.' }
    return [pscustomobject]@{
        Process = $process
        Stderr = $process.StandardError.ReadToEndAsync()
        Version = $ProtocolVersion
        NextId = 0
    }
}

function Send-McpRequest([object] $Session, [string] $Method, [hashtable] $Parameters) {
    $Session.NextId++
    $id = $Session.NextId
    if ($Session.Version -eq '2026-07-28') {
        $Parameters['_meta'] = @{
            'io.modelcontextprotocol/protocolVersion' = $Session.Version
            'io.modelcontextprotocol/clientCapabilities' = @{}
            'io.modelcontextprotocol/clientInfo' = @{ name = 'foundation-baseline'; version = '1.0' }
        }
    }
    $message = @{ jsonrpc = '2.0'; id = $id; method = $Method; params = $Parameters }
    $Session.Process.StandardInput.WriteLine(($message | ConvertTo-Json -Depth 50 -Compress))
    $Session.Process.StandardInput.Flush()
    while ($true) {
        $line = $Session.Process.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(30)).GetAwaiter().GetResult()
        if ($null -eq $line) { throw "MCP process exited before response $id. $($Session.Stderr.GetAwaiter().GetResult())" }
        $response = $line | ConvertFrom-Json -Depth 100
        if ($response.jsonrpc -ne '2.0') { throw "Invalid JSON-RPC output: $line" }
        if ($response.id -eq $id) { return $response }
    }
}

function Initialize-Mcp([object] $Session) {
    if ($Session.Version -eq '2026-07-28') {
        $response = Send-McpRequest $Session 'server/discover' @{}
    } else {
        $response = Send-McpRequest $Session 'initialize' @{
            protocolVersion = $Session.Version
            capabilities = @{}
            clientInfo = @{ name = 'foundation-baseline'; version = '1.0' }
        }
        if ($response.result.protocolVersion -ne $Session.Version) { throw 'Unexpected protocol version.' }
        $notification = @{ jsonrpc = '2.0'; method = 'notifications/initialized'; params = @{} }
        $Session.Process.StandardInput.WriteLine(($notification | ConvertTo-Json -Depth 20 -Compress))
        $Session.Process.StandardInput.Flush()
    }
    if ($response.error) { throw ($response | ConvertTo-Json -Depth 20 -Compress) }
}

function Invoke-McpTool([object] $Session, [string] $Name, [hashtable] $Request) {
    $response = Send-McpRequest $Session 'tools/call' @{ name = $Name; arguments = @{ request = $Request } }
    if ($response.error -or $response.result.isError) { throw ($response | ConvertTo-Json -Depth 30 -Compress) }
    return $response.result.structuredContent
}

function Stop-Mcp([object] $Session) {
    if ($null -eq $Session) { return }
    if (-not $Session.Process.HasExited) {
        $Session.Process.StandardInput.Close()
        if (-not $Session.Process.WaitForExit(10000)) { $Session.Process.Kill($true); $Session.Process.WaitForExit(5000) | Out-Null }
    }
    $stderrText = $Session.Stderr.GetAwaiter().GetResult()
    $Session.Process.Dispose()
    return $stderrText
}

function Get-Shape([object] $Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string]) { return 'string' }
    if ($Value -is [bool]) { return 'boolean' }
    if ($Value -is [byte] -or $Value -is [int16] -or $Value -is [int32] -or $Value -is [int64] -or $Value -is [decimal] -or $Value -is [double]) { return 'number' }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [Collections.IDictionary] -and $Value -isnot [pscustomobject]) {
        $items = @($Value)
        return [ordered]@{ type = 'array'; item = if ($items.Count) { Get-Shape $items[0] } else { 'unknown' } }
    }
    $properties = [ordered]@{}
    foreach ($property in $Value.PSObject.Properties | Sort-Object Name) { $properties[$property.Name] = Get-Shape $property.Value }
    return [ordered]@{ type = 'object'; properties = $properties }
}

function Invoke-Capture {
    Write-BuildIndependentEvidence
    $restore = Invoke-Recorded 'current-solution-restore' 'dotnet' @('restore', 'AgentMemoryMCP.slnx', '--locked-mode')

    if ($SkipBuildDependentChecks) {
    Add-Result 'build-dependent-checks' 'skipped' '' 0 'Build-dependent checks intentionally skipped.' 'Current post-migration restore'
    } else {
    $build = Invoke-Recorded 'current-release-build' 'dotnet' @('build', 'AgentMemoryMCP.slnx', '-c', $Configuration, '--no-restore')
    $test = Invoke-Recorded 'current-focused-contract-tests' 'dotnet' @('test', 'tests/AgentSession.MCP.ContractTests/AgentSession.MCP.ContractTests.csproj', '-c', $Configuration, '--no-restore')

    $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('agent-memory-foundation-' + [Guid]::NewGuid().ToString('N'))
    $tempRoot = [IO.Path]::GetFullPath($tempRoot)
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    try {
        $packRoot = Join-Path $tempRoot 'pack'
        $publishRoot = Join-Path $tempRoot 'publish'
        $storageRoot = Join-Path $tempRoot 'storage'
        New-Item -ItemType Directory -Path $packRoot, $publishRoot, $storageRoot -Force | Out-Null
        $pack = Invoke-Recorded 'stdio-package' 'dotnet' @('pack', 'src/AgentSession.MCP.StdioHost/AgentSession.MCP.StdioHost.csproj', '-c', $Configuration, '--no-restore', '-o', $packRoot)
        $publish = Invoke-Recorded 'stdio-publish' 'dotnet' @('publish', 'src/AgentSession.MCP.StdioHost/AgentSession.MCP.StdioHost.csproj', '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', '--no-restore', '-o', $publishRoot)

        $packagePath = Get-ChildItem -LiteralPath $packRoot -Filter '*.nupkg' -File | Select-Object -First 1
        if ($packagePath) {
            $expanded = Join-Path $tempRoot 'package'
            Expand-Archive -LiteralPath $packagePath.FullName -DestinationPath $expanded
            $nuspecPath = Get-ChildItem -LiteralPath $expanded -Filter '*.nuspec' -File | Select-Object -First 1
            $packageEvidence = Get-Content -LiteralPath (Join-Path $evidencePath 'package-and-command.json') -Raw | ConvertFrom-Json -Depth 100
            $packageEvidence.packageInspection = [ordered]@{
                status = 'passed'
                files = @(Get-ChildItem -LiteralPath $expanded -Recurse -File | ForEach-Object { [IO.Path]::GetRelativePath($expanded, $_.FullName).Replace('\', '/') } | Sort-Object)
                nuspec = if ($nuspecPath) { Get-Content -LiteralPath $nuspecPath.FullName -Raw } else { $null }
            }
            Write-JsonFile (Join-Path $evidencePath 'package-and-command.json') $packageEvidence
            Add-Result 'package-metadata-inspection' 'passed' 'inspect temporary nupkg as ZIP' 0 "Inspected $($packageEvidence.packageInspection.files.Count) packaged files."
        } else {
            Add-Result 'package-metadata-inspection' 'failed-regression' 'inspect temporary nupkg as ZIP' 1 'No package was produced.'
        }

        $executable = Join-Path $publishRoot 'AgentSession.MCP.StdioHost.exe'
        if ($publish.ExitCode -eq 0 -and (Test-Path -LiteralPath $executable -PathType Leaf)) {
            foreach ($protocol in @('2025-11-25', '2026-07-28')) {
                $smokeRoot = Join-Path $tempRoot ('smoke-' + $protocol)
                $smoke = Invoke-Recorded "published-smoke-$protocol" 'pwsh' @('-NoProfile', '-File', 'scripts/Test-PublishedMcp.ps1', '-Executable', $executable, '-SystemRoot', $smokeRoot, '-ProtocolVersion', $protocol)
            }

            $discoveryVersions = [Collections.Generic.List[object]]::new()
            foreach ($protocol in @('2025-06-18', '2025-11-25', '2026-07-28')) {
                $session = $null
                try {
                    $session = New-McpProcess $executable (Join-Path $tempRoot ('discovery-' + $protocol)) 'foundation-baseline' $protocol
                    Initialize-Mcp $session
                    $listed = Send-McpRequest $session 'tools/list' @{}
                    if ($listed.error) { throw ($listed | ConvertTo-Json -Depth 30 -Compress) }
                    $tools = @($listed.result.tools | Sort-Object name)
                    $names = @($tools.name)
                    if ($names.Count -ne 26 -or @($names | Sort-Object -Unique).Count -ne 26) { throw "Expected 26 unique tools, found $($names.Count)." }
                    foreach ($tool in $tools) {
                        foreach ($requiredProperty in @('name', 'description', 'annotations', 'inputSchema', 'outputSchema')) {
                            if ($tool.PSObject.Properties.Name -notcontains $requiredProperty -or $null -eq $tool.$requiredProperty) {
                                throw "Tool '$($tool.name)' is missing required discovery property '$requiredProperty'."
                            }
                        }
                    }
                    $sessionResult = Invoke-McpTool $session 'create_or_activate_session' @{ sessionId = 'shape-session'; actorId = 'shape-agent'; operationId = "shape-$protocol" }
                    $memoryResult = Invoke-McpTool $session 'memory_remember' @{
                        operationId = "shape-memory-$($protocol.Replace('-', ''))"
                        tier = 'short'
                        content = 'Synthetic discovery-shape memory.'
                        sessionId = 'shape-session'
                        agentId = 'shape-agent'
                    }
                    $discoveryVersions.Add([ordered]@{
                        protocolVersion = $protocol
                        tools = @($tools | ForEach-Object { [ordered]@{
                            name = $_.name
                            description = $_.description
                            annotations = $_.annotations
                            inputSchema = $_.inputSchema
                            outputSchema = $_.outputSchema
                        } })
                        representativeStructuredResults = [ordered]@{
                            session = Get-Shape $sessionResult
                            agentMemory = Get-Shape $memoryResult
                        }
                    })
                } finally { $null = Stop-Mcp $session }
            }
            Write-JsonFile (Join-Path $evidencePath 'mcp-discovery.json') ([ordered]@{
                schemaVersion = 1
                uniqueToolCount = 26
                protocolVersions = @($discoveryVersions)
            })
            Add-Result 'mcp-discovery-contract' 'passed' 'initialize/discover and tools/list for three protocol versions' 0 'Captured 26 unique tools with descriptions, annotations, full schemas, and representative structured-result shapes.'

            $fixtureSession = $null
            $memoryId = $null
            try {
                $fixtureSession = New-McpProcess $executable $storageRoot 'foundation-baseline' '2025-11-25'
                Initialize-Mcp $fixtureSession
                $null = Invoke-McpTool $fixtureSession 'create_or_activate_session' @{ sessionId = 'foundation-session'; actorId = 'agent-a'; operationId = 'activate' }
                $null = Invoke-McpTool $fixtureSession 'append_agent_memory' @{
                    sessionId = 'foundation-session'; actorId = 'agent-a'; operationId = 'append'; updates = @(@{
                        expectedRevision = 0; artifact = @{ id = 'context'; kind = 'context'; actorId = 'agent-a'; title = 'Foundation context'; context = @{ objective = 'Preserve modular-foundation compatibility'; nextActions = @('Verify restart') } }
                    })
                }
                $null = Invoke-McpTool $fixtureSession 'coordinate_agent_task' @{ sessionId = 'foundation-session'; actorId = 'agent-a'; operationId = 'coordinate'; taskId = 'foundation-task'; action = 'create'; workKey = 'foundation'; title = 'Foundation task' }
                $remembered = Invoke-McpTool $fixtureSession 'memory_remember' @{ operationId = 'remember'; tier = 'short'; content = 'Synthetic foundation compatibility memory.'; sessionId = 'foundation-session'; taskId = 'foundation-task'; agentId = 'agent-a' }
                $memoryId = $remembered.memoryId
                $cursor = $null
                do {
                    $request = @{ sessionId = 'foundation-session'; actorId = 'reader'; pageSize = 1 }
                    if ($cursor) { $request.cursor = $cursor }
                    $page = Invoke-McpTool $fixtureSession 'resume_agent_session' $request
                    $cursor = $page.cursor
                } while ($page.hasMore)
                $null = Invoke-McpTool $fixtureSession 'checkpoint_agent_session' @{ sessionId = 'foundation-session'; actorId = 'reader'; snapshotId = $page.snapshotId; sequence = $page.sequence }
            } finally { $null = Stop-Mcp $fixtureSession }

            $restart = $null
            try {
                $restart = New-McpProcess $executable $storageRoot 'foundation-baseline' '2025-11-25'
                Initialize-Mcp $restart
                $resumed = Invoke-McpTool $restart 'resume_agent_session' @{ sessionId = 'foundation-session'; actorId = 'reader' }
                $reread = Invoke-McpTool $restart 'memory_get' @{ memoryId = $memoryId }
                if ($resumed.acknowledgedSequence -lt 1 -or $reread.availability -ne 'available') { throw 'Restarted process could not read the checkpointed session and memory.' }
            } finally { $null = Stop-Mcp $restart }

            $canonicalStorageRoot = Assert-ContainedPath (Join-Path $evidencePath 'canonical-storage') $repositoryRoot 'Canonical storage evidence root'
            $fixtureRoot = Assert-ContainedPath (Join-Path $canonicalStorageRoot 'fixture') $canonicalStorageRoot 'Canonical storage fixture root'
            if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
            New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
            foreach ($relative in @('sessions/foundation-baseline', 'repositories/foundation-baseline')) {
                $source = Join-Path $storageRoot $relative
                if (Test-Path -LiteralPath $source) {
                    $destination = Join-Path $fixtureRoot $relative
                    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
                    Copy-Item -LiteralPath $source -Destination $destination -Recurse
                }
            }
            $files = @(
                Get-ChildItem -LiteralPath $fixtureRoot -Recurse -File |
                    ForEach-Object { [ordered]@{
                        path = [IO.Path]::GetRelativePath($fixtureRoot, $_.FullName).Replace('\', '/')
                        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                        bytes = $_.Length
                    } } |
                    Sort-Object path
            )
            $forbiddenFixtureValues = @(
                $repositoryRoot,
                [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile),
                $tempRoot
            ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            foreach ($fixtureFile in Get-ChildItem -LiteralPath $fixtureRoot -Recurse -File) {
                $fixtureText = Get-Content -LiteralPath $fixtureFile.FullName -Raw
                foreach ($forbiddenValue in $forbiddenFixtureValues) {
                    if ($fixtureText.Contains($forbiddenValue, $pathComparison)) {
                        throw "Canonical fixture contains a forbidden machine-specific path in '$([IO.Path]::GetRelativePath($fixtureRoot, $fixtureFile.FullName))'."
                    }
                }
            }
            Write-JsonFile (Join-Path $evidencePath 'canonical-storage/manifest.json') ([ordered]@{
                schemaVersion = 1
                storageRoot = '${FIXTURE_ROOT}'
                repositoryId = 'foundation-baseline'
                sessionId = 'foundation-session'
                memoryId = $memoryId
                providerRequirement = 'none'
                restartRead = 'passed'
                cursorCheckpoint = 'passed-final-page-only'
                files = $files
            })
            Add-Result 'canonical-storage-restart-fixture' 'passed' 'isolated MCP create/page/checkpoint/restart/read flow' 0 "Captured $($files.Count) repository-scoped fixture files without Ollama or Qdrant."
        } else {
            foreach ($name in @('published-smoke-2025-11-25', 'published-smoke-2026-07-28', 'mcp-discovery-contract', 'canonical-storage-restart-fixture')) {
                Add-Result $name 'skipped' '' 0 'Published executable unavailable.' 'stdio-publish'
            }
        }
    } finally {
        $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if ($tempRoot.StartsWith($tempParent, $pathComparison) -and (Split-Path -Leaf $tempRoot).StartsWith('agent-memory-foundation-')) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            throw "Refusing to clean unvalidated temporary path: $tempRoot"
        }
    }
}
}

$captureError = $null
try {
    Invoke-Capture
} catch {
    $captureError = $_
    Add-Result 'baseline-capture-unhandled' 'failed-regression' 'Capture-StandaloneFoundationBaseline.ps1' 1 $_.Exception.Message
}

Write-JsonFile (Join-Path $evidencePath 'verification-results.json') ([ordered]@{
    schemaVersion = 1
    configuration = $Configuration
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    results = @($results)
})

$summaryPath = Join-Path (Split-Path -Parent $evidencePath) 'baseline-verification.md'
$summary = @(
    '# Standalone modular foundation baseline verification',
    '',
    '- Evidence was generated from the current post-migration tree.',
    '- The StdioHost project is the package and command owner; deleted legacy compatibility source is not an owner or build target.',
    '- Package/feed publication is not performed by this capture script.',
    '',
    '## Captured results',
    ''
)
foreach ($result in $results) { $summary += "- $($result.name): **$($result.status)** (exit $($result.exitCode))" }
$summary += ''
$summary += 'Generated by `scripts/Capture-StandaloneFoundationBaseline.ps1`; machine-readable detail is in `baselines/verification-results.json`.'
[IO.File]::WriteAllLines($summaryPath, $summary, [Text.UTF8Encoding]::new($false))

$failed = @($results | Where-Object status -eq 'failed-regression')
if ($failed.Count) { throw "Baseline capture recorded $($failed.Count) regression failure(s): $($failed.name -join ', ')" }
if ($captureError) { throw $captureError }
Write-Output "Standalone foundation baseline captured: $($results.Count) results; evidence $EvidenceRoot"
