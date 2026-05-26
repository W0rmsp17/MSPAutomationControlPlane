[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:7071/api",
    [string]$ImportRequestPath = "samples/import-account-management-report-module.json",
    [string]$AccessToken = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedImportRequestPath = Resolve-Path (Join-Path $repoRoot $ImportRequestPath) -ErrorAction Stop

$headers = @{}
if (-not [string]::IsNullOrWhiteSpace($AccessToken)) {
    $headers.Authorization = "Bearer $AccessToken"
}

function Invoke-ControlPlaneApi {
    param(
        [Parameter(Mandatory)]
        [string]$Method,

        [Parameter(Mandatory)]
        [string]$Path,

        [object]$Body = $null
    )

    $uri = "$($ApiBaseUrl.TrimEnd('/'))/$($Path.TrimStart('/'))"
    $arguments = @{
        Uri         = $uri
        Method      = $Method
        Headers     = $headers
        ErrorAction = "Stop"
    }

    if ($null -ne $Body) {
        $arguments.ContentType = "application/json"
        $arguments.Body = $Body
    }

    return Invoke-RestMethod @arguments
}

$body = Get-Content -LiteralPath $resolvedImportRequestPath -Raw

try {
    $registration = Invoke-ControlPlaneApi -Method "Post" -Path "modules/import" -Body $body
    Write-Host "Module imported: $($registration.manifest.id) $($registration.manifest.version)" -ForegroundColor Green
}
catch {
    $errorText = $_.ErrorDetails.Message
    if ([string]::IsNullOrWhiteSpace($errorText)) {
        $errorText = $_.Exception.Message
    }

    if ($errorText -like "*already registered*") {
        Write-Host "Module already registered; reusing existing record."
    }
    else {
        throw $errorText
    }
}

$modules = Invoke-ControlPlaneApi -Method "Get" -Path "modules"
$match = $modules | Where-Object {
    $_.manifest.id -eq "msp-account-management-report" -and
    $_.manifest.version -eq "0.1.0"
} | Select-Object -First 1

if ($null -eq $match) {
    throw "Imported module was not found in module catalog."
}

Write-Host "Module catalog contains msp-account-management-report 0.1.0." -ForegroundColor Green
