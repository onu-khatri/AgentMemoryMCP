[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $BackupPath,
    [Parameter(Mandatory)] [string] $SystemRoot,
    [Parameter(Mandatory)] [switch] $ConfirmRestore
)

$ErrorActionPreference = 'Stop'
if (-not $ConfirmRestore) { throw 'Pass -ConfirmRestore after reviewing the target path.' }
$archive = [IO.Path]::GetFullPath($BackupPath)
$root = [IO.Path]::GetFullPath($SystemRoot)
if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) { throw 'Backup archive does not exist.' }
if (Test-Path -LiteralPath $root) { throw 'Restore target must not already exist.' }
$parent = Split-Path -Parent $root
if (-not $parent) { throw 'Restore target must be below a parent directory.' }
New-Item -ItemType Directory -Path $parent -Force | Out-Null
$staging = $root + '.restore-' + [guid]::NewGuid().ToString('N')
New-Item -ItemType Directory -Path $staging | Out-Null
try {
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($entry in $zip.Entries) {
            $target = [IO.Path]::GetFullPath((Join-Path $staging $entry.FullName))
            if (-not $target.StartsWith($staging + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Backup contains a path outside the restore target.'
            }
            if ([string]::IsNullOrEmpty($entry.Name)) {
                New-Item -ItemType Directory -Path $target -Force | Out-Null
                continue
            }
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
        }
    } finally { $zip.Dispose() }
    Move-Item -LiteralPath $staging -Destination $root
} finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
Write-Output "AgentMemory backup restored to new root: $root"

