[CmdletBinding()]
param(
    [string] $SystemRoot = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex/AgentMemory'),
    [ValidateRange(1, 65535)] [int] $RestPort = 6333,
    [ValidateRange(1, 65535)] [int] $GrpcPort = 6334,
    [ValidatePattern('^[a-z0-9][a-z0-9-]*$')] [string] $ProjectName = 'agent-memory-qdrant'
)

$ErrorActionPreference = 'Stop'
$compose = Join-Path $PSScriptRoot '..\docker-compose.ai-learning.yml'
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker CLI is required. This script does not install Docker.'
}
$env:AGENT_MEMORY_ROOT = [IO.Path]::GetFullPath($SystemRoot).Replace('\', '/')
$env:QDRANT_REST_PORT = $RestPort.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:QDRANT_GRPC_PORT = $GrpcPort.ToString([Globalization.CultureInfo]::InvariantCulture)
docker compose --project-name $ProjectName --file $compose config
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose configuration rendering failed.' }
