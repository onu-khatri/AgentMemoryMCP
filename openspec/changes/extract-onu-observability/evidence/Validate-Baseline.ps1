[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$changeRoot = Split-Path -Parent $PSScriptRoot
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $changeRoot '..\..\..'))
$mapPath = Join-Path $PSScriptRoot 'migration-map.json'
$configurationPath = Join-Path $PSScriptRoot 'baselines\mcp-configuration.json'
$classificationPath = Join-Path $PSScriptRoot 'baselines\mcp-classification.json'
$telemetryPath = Join-Path $PSScriptRoot 'baselines\mcp-telemetry.json'

$map = Get-Content -Raw -LiteralPath $mapPath | ConvertFrom-Json -Depth 100
$configuration = Get-Content -Raw -LiteralPath $configurationPath | ConvertFrom-Json -Depth 100
$classification = Get-Content -Raw -LiteralPath $classificationPath | ConvertFrom-Json -Depth 100
$telemetry = Get-Content -Raw -LiteralPath $telemetryPath | ConvertFrom-Json -Depth 100

$actualSources = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'AgentSession.MCP\Observability') -Filter '*.cs' -File |
    ForEach-Object { 'AgentSession.MCP/Observability/' + $_.Name } |
    Sort-Object)
$mappedSources = @($map.observabilitySources.path | Sort-Object)
if ($actualSources.Count -ne 22) {
    throw "Expected 22 current observability source files, found $($actualSources.Count)."
}
if (Compare-Object $actualSources $mappedSources) {
    throw 'The observability source migration map does not exactly match the current source directory.'
}

$inventoryGroups = @(
    'observabilitySources',
    'registrationAndOptions',
    'applicationSources',
    'tests',
    'documents',
    'scripts',
    'collectorAssets'
)
$inventoryPaths = foreach ($group in $inventoryGroups) { @($map.$group.path) }
$duplicates = @($inventoryPaths | Group-Object | Where-Object Count -gt 1)
if ($duplicates.Count -ne 0) {
    throw "Inventory paths must appear once; duplicates: $($duplicates.Name -join ', ')."
}
foreach ($relativePath in $inventoryPaths) {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $relativePath) -PathType Leaf)) {
        throw "Mapped path does not exist: $relativePath"
    }
}

if (@($configuration.options).Count -ne 25) {
    throw 'Configuration baseline must contain the 24 typed options plus MinimumLogLevel.'
}
if (@($classification.outcomes).Count -ne 9) {
    throw 'Classification baseline must contain nine outcomes.'
}
if (@($classification.registeredTools).Count -ne 26) {
    throw 'Classification baseline must contain 26 registered tools.'
}
if (@($telemetry.metrics).Count -ne 17) {
    throw 'Telemetry baseline must contain 17 metric instruments.'
}
if (@($telemetry.events).Count -ne 5) {
    throw 'Telemetry baseline must contain five registered/direct diagnostic events.'
}

$sourceText = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'AgentSession.MCP\Observability\TelemetrySchema.cs')
$metricText = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'AgentSession.MCP\Observability\McpTelemetry.cs')
$eventText = (Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'AgentSession.MCP\Observability\SafeLogEvents.cs')) +
    (Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'AgentSession.MCP\Observability\TelemetryExportHealth.cs'))
$specText = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'openspec\specs\mcp-observability\spec.md')
$docsText = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'docs\observability\configuration-and-signals.md')

foreach ($metric in $telemetry.metrics) {
    if (-not $metricText.Contains($metric.name) -or -not $docsText.Contains($metric.name)) {
        throw "Metric is not present in both implementation and canonical catalog: $($metric.name)"
    }
}
foreach ($event in $telemetry.events) {
    if (-not $eventText.Contains($event.name) -or -not $docsText.Contains($event.name)) {
        throw "Event is not present in both implementation and canonical catalog: $($event.name)"
    }
}
foreach ($value in @($classification.errorCodes + $classification.dependencyTypes + $classification.dependencyOperations + $classification.dropReasons)) {
    if ($value -notin @('other') -and -not $sourceText.Contains($value)) {
        throw "Bounded classification value is absent from TelemetrySchema: $value"
    }
}
foreach ($requiredPhrase in @('Low-cardinality metrics catalog', 'Validated and versioned observability configuration', 'Stable outcomes and error telemetry', 'Metadata-only privacy and security policy')) {
    if (-not $specText.Contains($requiredPhrase)) {
        throw "Compatibility spec requirement is missing: $requiredPhrase"
    }
}

$requiredTests = @(
    'AgentSession.MCP.Tests/ObservabilityFoundationTests.cs',
    'AgentSession.MCP.Tests/ObservabilityBoundaryTests.cs',
    'AgentSession.MCP.Tests/ObservabilityBackgroundTests.cs',
    'AgentSession.MCP.Tests/CollectorIntegrationTests.cs'
)
foreach ($test in $requiredTests) {
    if ($test -notin @($map.tests.path)) {
        throw "Required observability test is absent from the migration map: $test"
    }
}

[pscustomobject]@{
    observabilitySources = $actualSources.Count
    mappedInventoryPaths = $inventoryPaths.Count
    options = @($configuration.options).Count
    outcomes = @($classification.outcomes).Count
    registeredTools = @($classification.registeredTools).Count
    metrics = @($telemetry.metrics).Count
    events = @($telemetry.events).Count
    compatibilitySpec = 'matched'
} | ConvertTo-Json
