[CmdletBinding()]
param(
    [ValidateSet('Local', 'Production', 'Persistent')] [string] $Mode = 'Local',
    [ValidatePattern('^[a-z0-9][a-z0-9-]*$')] [string] $ProjectName = 'agent-memory-observability'
)

$ErrorActionPreference = 'Stop'
$observability = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\observability'))
$compose = Join-Path $observability 'docker-compose.yml'
$persistentCompose = Join-Path $observability 'docker-compose.persistent.yml'
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker CLI is required. This script does not install Docker.'
}

$env:OTEL_COLLECTOR_CONFIG = switch ($Mode) {
    'Local' { 'collector.local.yaml' }
    'Production' { 'collector.production.yaml' }
    'Persistent' { 'collector.persistent.yaml' }
}
$arguments = @('compose', '--project-name', $ProjectName, '--project-directory', $observability, '--file', $compose)
if ($Mode -eq 'Persistent') { $arguments += @('--file', $persistentCompose) }
$arguments += 'down'
& docker @arguments
if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed to stop project $ProjectName." }

$retention = if ($Mode -eq 'Persistent') {
    'Persistent queue volume was retained; remove it only through an approved secure-deletion procedure.'
} else {
    'The local tmpfs sink was removed with the container; no telemetry volume was retained.'
}
Write-Output "Observability project $ProjectName is stopped. $retention"
