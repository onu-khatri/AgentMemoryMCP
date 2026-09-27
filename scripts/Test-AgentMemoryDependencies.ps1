[CmdletBinding()]
param(
    [string] $SystemRoot = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex/AgentMemory'),
    [ValidateRange(1, 65535)] [int] $RestPort = 6333,
    [ValidateRange(1, 65535)] [int] $GrpcPort = 6334,
    [ValidateRange(1, 65535)] [int] $OllamaPort = 11434,
    [string] $EmbeddingModel = 'embeddinggemma'
)

$ErrorActionPreference = 'Stop'
$resolvedRoot = [IO.Path]::GetFullPath($SystemRoot)
$vectorRoot = Join-Path $resolvedRoot '.vector\qdrant'
$storageWritable = $false
if (Test-Path -LiteralPath $vectorRoot) {
    $probe = Join-Path $vectorRoot ('.write-probe-' + [guid]::NewGuid().ToString('N'))
    try {
        [IO.File]::WriteAllText($probe, 'probe')
        Remove-Item -LiteralPath $probe -Force
        $storageWritable = $true
    } catch { $storageWritable = $false }
}

$qdrant = $null
try { $qdrant = Invoke-RestMethod -Uri "http://127.0.0.1:$RestPort/" -TimeoutSec 5 }
catch { }
$grpc = Test-NetConnection -ComputerName 127.0.0.1 -Port $GrpcPort -InformationLevel Quiet -WarningAction SilentlyContinue
$models = @()
try {
    $tags = Invoke-RestMethod -Uri "http://127.0.0.1:$OllamaPort/api/tags" -TimeoutSec 5
    $models = @($tags.models | ForEach-Object { $_.name })
} catch { }

[pscustomobject]@{
    systemRoot = $resolvedRoot
    vectorRoot = $vectorRoot
    storageWritable = $storageWritable
    qdrantRestHealthy = ($null -ne $qdrant)
    qdrantGrpcReachable = [bool]$grpc
    ollamaHealthy = ($models.Count -gt 0)
    embeddingModelAvailable = [bool]($models -contains $EmbeddingModel -or $models -contains ($EmbeddingModel + ':latest'))
    models = $models
} | ConvertTo-Json -Depth 4

