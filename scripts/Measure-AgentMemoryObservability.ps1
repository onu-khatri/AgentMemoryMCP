[CmdletBinding()]
param(
    [ValidateRange(25, 10000)] [int] $Iterations = 250,
    [string] $OutputPath = '.tmp/observability-benchmark.json',
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repository 'AgentSession.MCP.Benchmarks/AgentSession.MCP.Benchmarks.csproj'
$output = if ([IO.Path]::IsPathRooted($OutputPath)) {
    [IO.Path]::GetFullPath($OutputPath)
} else {
    [IO.Path]::GetFullPath((Join-Path $repository $OutputPath))
}
$outputDirectory = Split-Path -Parent $output
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

dotnet run --project $project --configuration $Configuration -- $Iterations $output
if ($LASTEXITCODE -ne 0) {
    throw "Observability benchmark failed its provisional smoke budget. See $output."
}
Write-Output "Observability benchmark passed its provisional smoke budget: $output"
