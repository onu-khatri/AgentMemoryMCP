[CmdletBinding()]
param(
    [ValidateSet('Local', 'Production', 'Persistent')] [string] $Mode = 'Local',
    [ValidateRange(1, 65535)] [int] $HealthPort = 13133,
    [ValidateRange(1, 65535)] [int] $MetricsPort = 8888,
    [ValidatePattern('^[a-z0-9][a-z0-9-]*$')] [string] $ProjectName = 'agent-memory-observability'
)

$ErrorActionPreference = 'Stop'
$dockerCli = $null -ne (Get-Command docker -ErrorAction SilentlyContinue)
$composeAvailable = $false
$daemonAvailable = $false
$containerRunning = $false
$persistentVolume = $null
$persistentMountPath = $null
$persistentInitCompleted = $false
$messages = [Collections.Generic.List[string]]::new()

if (-not $dockerCli) {
    $messages.Add('Install Docker Desktop or Docker Engine with the Compose plugin; this repository does not install it.')
} else {
    docker compose version *> $null
    $composeAvailable = $LASTEXITCODE -eq 0
    if (-not $composeAvailable) { $messages.Add('Docker Compose plugin is unavailable.') }
    docker info *> $null
    $daemonAvailable = $LASTEXITCODE -eq 0
    if (-not $daemonAvailable) { $messages.Add('Docker daemon is unavailable or access is denied; start Docker and verify current-user access.') }
    if ($daemonAvailable) {
        $containerId = docker ps --filter "label=com.docker.compose.project=$ProjectName" --filter 'label=com.docker.compose.service=collector' --format '{{.ID}}' | Select-Object -First 1
        $containerRunning = -not [string]::IsNullOrWhiteSpace($containerId)
        if (-not $containerRunning) { $messages.Add("Collector container for project $ProjectName is not running.") }
        if ($Mode -eq 'Persistent' -and $containerRunning) {
            $mount = docker inspect $containerId --format '{{range .Mounts}}{{if eq .Destination "/var/lib/otelcol/storage"}}{{.Name}}|{{.Destination}}{{end}}{{end}}'
            if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($mount)) {
                $parts = $mount -split '\|', 2
                $persistentVolume = $parts[0]
                $persistentMountPath = $parts[1]
            }
            $initId = docker ps --all --filter "label=com.docker.compose.project=$ProjectName" --filter 'label=com.docker.compose.service=queue-init' --format '{{.ID}}' | Select-Object -First 1
            if (-not [string]::IsNullOrWhiteSpace($initId)) {
                $initExitCode = docker inspect $initId --format '{{.State.ExitCode}}'
                $persistentInitCompleted = $LASTEXITCODE -eq 0 -and $initExitCode -eq '0'
            }
        }
    }
}

$health = $false
try {
    $healthResponse = Invoke-WebRequest -Uri "http://127.0.0.1:$HealthPort/" -TimeoutSec 3 -UseBasicParsing
    $health = $healthResponse.StatusCode -eq 200
} catch { $messages.Add("Collector health endpoint http://127.0.0.1:$HealthPort/ is unavailable.") }

$metricsReachable = $false
$metricNames = @()
$metricSummary = [ordered]@{
    accepted = $false
    refused = $false
    exporterFailures = $false
    queueSize = $false
    queueCapacity = $false
    memory = $false
    storage = $false
}
try {
    $metrics = (Invoke-WebRequest -Uri "http://127.0.0.1:$MetricsPort/metrics" -TimeoutSec 3 -UseBasicParsing).Content
    $metricsReachable = $true
    $metricSummary.accepted = $metrics -match 'otelcol_receiver_accepted'
    $metricSummary.refused = $metrics -match 'otelcol_receiver_refused'
    $metricSummary.exporterFailures = $metrics -match 'otelcol_exporter_(send_failed|enqueue_failed)'
    $metricSummary.queueSize = $metrics -match 'otelcol_exporter_queue_size'
    $metricSummary.queueCapacity = $metrics -match 'otelcol_exporter_queue_capacity'
    $metricSummary.memory = $metrics -match 'otelcol_process_memory_rss'
    $metricSummary.storage = $metrics -match 'otelcol_extension_storage'
    $metricNames = @($metricSummary.Keys | Where-Object { $metricSummary[$_] })
} catch { $messages.Add("Collector self-metrics endpoint http://127.0.0.1:$MetricsPort/metrics is unavailable.") }

if ($Mode -eq 'Persistent') {
    $metricSummary.storage =
        -not [string]::IsNullOrWhiteSpace($persistentMountPath) -and
        $persistentInitCompleted -and
        $metricSummary.queueSize -and
        $metricSummary.queueCapacity
    if (-not $metricSummary.storage) {
        $messages.Add('Persistent queue mount, initializer, or queue metrics are incomplete; inspect collector logs and the named volume before relying on queued durability.')
    }
}
if ($Mode -ne 'Persistent') {
    $messages.Add('Default local mode uses a container tmpfs sink and retains no telemetry volume after stop.')
}
$metricNames = @($metricSummary.Keys | Where-Object { $metricSummary[$_] })

$operational = $dockerCli -and $composeAvailable -and $daemonAvailable -and $containerRunning -and $health -and $metricsReachable
[pscustomobject]@{
    mode = $Mode
    dockerCli = $dockerCli
    composeAvailable = $composeAvailable
    daemonAvailable = $daemonAvailable
    containerRunning = $containerRunning
    receiverHealthy = $health
    selfMetricsReachable = $metricsReachable
    observedMetricGroups = $metricNames
    queueAndDropEvidence = $metricSummary
    persistentQueueConfigured = ($Mode -eq 'Persistent')
    persistentStorage = if ($Mode -eq 'Persistent') { [ordered]@{
        volume = $persistentVolume
        mountPath = $persistentMountPath
        initializerCompleted = $persistentInitCompleted
        healthy = $metricSummary.storage
    } } else { $null }
    operational = $operational
    readiness = if ($operational) { 'operational_not_production_approved' } else { 'not_ready' }
    messages = $messages
} | ConvertTo-Json -Depth 5

if (-not $operational) { exit 2 }
