[CmdletBinding()]
param(
    [string] $SystemRoot = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex/AgentMemory'),
    [Parameter(Mandatory)] [ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')] [string] $RepositoryId,
    [Parameter(Mandatory)] [switch] $ConfirmReset
)

$ErrorActionPreference = 'Stop'
if (-not $ConfirmReset) { throw 'Pass -ConfirmReset after reviewing the repository identity.' }
$root = [IO.Path]::GetFullPath($SystemRoot)
$learning = [IO.Path]::GetFullPath((Join-Path $root "repositories\$RepositoryId\AiLearning"))
$expectedPrefix = [IO.Path]::GetFullPath((Join-Path $root 'repositories')) + [IO.Path]::DirectorySeparatorChar
if (-not $learning.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved learning path is outside the configured repository area.'
}
$targets = @(
    [IO.Path]::GetFullPath((Join-Path $learning '.index.json')),
    [IO.Path]::GetFullPath((Join-Path $learning 'indexes'))
)
foreach ($target in $targets) {
    if (-not $target.StartsWith($learning + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to reset a path outside repository learning storage.'
    }
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
}
Write-Output "Removed derived filesystem/vector metadata only for repository '$RepositoryId'. Canonical memory, archives, sessions and Qdrant collections were not deleted."
