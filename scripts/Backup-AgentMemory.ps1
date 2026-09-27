[CmdletBinding()]
param(
    [string] $SystemRoot = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex/AgentMemory'),
    [Parameter(Mandatory)] [string] $Destination,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($SystemRoot)
$archive = [IO.Path]::GetFullPath($Destination)
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'SystemRoot does not exist.' }
if ($archive.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Backup destination must be outside SystemRoot.'
}
if ((Test-Path -LiteralPath $archive) -and -not $Force) { throw 'Backup already exists; pass -Force to replace it.' }
$parent = Split-Path -Parent $archive
if (-not $parent) { throw 'Backup destination must include a parent directory.' }
New-Item -ItemType Directory -Path $parent -Force | Out-Null
$temporary = $archive + '.tmp-' + [guid]::NewGuid().ToString('N')
try {
    [IO.Compression.ZipFile]::CreateFromDirectory(
        $root,
        $temporary,
        [IO.Compression.CompressionLevel]::Optimal,
        $false
    )
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
    Move-Item -LiteralPath $temporary -Destination $archive
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
}
Write-Output "AgentMemory backup created: $archive"

