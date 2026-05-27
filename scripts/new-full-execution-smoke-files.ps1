[CmdletBinding()]
param(
    [string]$TemplatePath = "samples/full-execution-smoke.template.json",
    [string]$OutputDirectory = ".work/smoke",
    [string]$ClientConnectionFileName = "client-connection-real.json",
    [string]$JobRequestFileName = "submit-job-real.json",
    [switch]$Force
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedTemplatePath = Resolve-Path (Join-Path $repoRoot $TemplatePath) -ErrorAction Stop
$resolvedOutputDirectory = Join-Path $repoRoot $OutputDirectory

if (-not (Test-Path $resolvedOutputDirectory)) {
    New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null
}

$template = Get-Content -LiteralPath $resolvedTemplatePath -Raw | ConvertFrom-Json -ErrorAction Stop
if ($null -eq $template.clientConnection -or $null -eq $template.jobRequest) {
    throw "Template must contain clientConnection and jobRequest objects."
}

$clientConnectionPath = Join-Path $resolvedOutputDirectory $ClientConnectionFileName
$jobRequestPath = Join-Path $resolvedOutputDirectory $JobRequestFileName

foreach ($path in @($clientConnectionPath, $jobRequestPath)) {
    if ((Test-Path $path) -and -not $Force) {
        throw "Refusing to overwrite existing file '$path'. Re-run with -Force to replace it."
    }
}

$template.clientConnection |
    ConvertTo-Json -Depth 12 |
    Set-Content -LiteralPath $clientConnectionPath -Encoding utf8

$template.jobRequest |
    ConvertTo-Json -Depth 12 |
    Set-Content -LiteralPath $jobRequestPath -Encoding utf8

Write-Host "Created local smoke files:" -ForegroundColor Green
Write-Host "  Client connection: $clientConnectionPath"
Write-Host "  Job request:       $jobRequestPath"
Write-Host ""
$displayClientConnectionPath = Join-Path $OutputDirectory $ClientConnectionFileName
$displayJobRequestPath = Join-Path $OutputDirectory $JobRequestFileName
Write-Host "Edit both files and replace placeholder values before running:"
Write-Host "  .\scripts\test-cloud-smoke.ps1 -ClientConnectionPath `"$displayClientConnectionPath`" -JobRequestPath `"$displayJobRequestPath`""
