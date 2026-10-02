[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifestPath = Join-Path $repositoryRoot 'observability/asset-ownership.json'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

if ($manifest.schemaVersion -ne 1) {
    throw "Unsupported observability asset ownership schema: $($manifest.schemaVersion)."
}

$declared = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($asset in $manifest.assets) {
    $relativePath = [string]$asset.path
    if (-not $declared.Add($relativePath)) {
        throw "Duplicate observability asset ownership entry: $relativePath."
    }

    $resolved = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $relativePath))
    if (-not $resolved.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Observability asset path escapes the repository: $relativePath."
    }
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "Declared observability asset does not exist: $relativePath."
    }

    if ($asset.ownership -eq 'versioned-package-copy') {
        $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolved).Hash.ToLowerInvariant()
        if ($actualHash -ne $asset.sha256) {
            throw "Versioned package copy drifted without an ownership-manifest update: $relativePath."
        }

        $source = [IO.Path]::GetFullPath((Join-Path $repositoryRoot ([string]$asset.source)))
        if (Test-Path -LiteralPath $source -PathType Leaf) {
            $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash.ToLowerInvariant()
            if ($sourceHash -ne $actualHash) {
                throw "Versioned package copy no longer matches its checked-out source: $relativePath."
            }
        }
    }
    elseif ($asset.ownership -ne 'agentmemory-service-owned') {
        throw "Unknown observability asset ownership value '$($asset.ownership)' for $relativePath."
    }
}

$expectedPaths = @(
    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'observability') -File |
        Where-Object Extension -in '.yaml', '.yml' |
        ForEach-Object { 'observability/' + $_.Name }
    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'scripts') -File -Filter '*AgentMemoryObservability.ps1' |
        ForEach-Object { 'scripts/' + $_.Name }
)
foreach ($path in $expectedPaths) {
    if (-not $declared.Contains($path)) {
        throw "Observability deployment asset has no ownership declaration: $path."
    }
}

[xml]$consumerProject = Get-Content -LiteralPath (Join-Path $repositoryRoot 'AgentSession.MCP/AgentSession.MCP.csproj')
$packageVersions = @($consumerProject.SelectNodes('//PackageReference[starts-with(@Include, "OnuObservability.")]') |
    ForEach-Object { [string]$_.Version } |
    Sort-Object -Unique)
if ($packageVersions.Count -ne 1 -or $packageVersions[0] -ne $manifest.sourcePackageVersion) {
    throw "Observability assets target $($manifest.sourcePackageVersion), but AgentMemory consumes $($packageVersions -join ', ')."
}

[ordered]@{
    packageVersion = $manifest.sourcePackageVersion
    policy = $manifest.policy
    declaredAssets = $declared.Count
    packageCopies = @($manifest.assets | Where-Object ownership -eq 'versioned-package-copy').Count
    serviceOwned = @($manifest.assets | Where-Object ownership -eq 'agentmemory-service-owned').Count
} | ConvertTo-Json
