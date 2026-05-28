[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:7071/api",
    [string]$ImportRequestPath = "samples/import-account-management-report-module.json",
    [string]$AccessToken = "",
    [string]$ExpectedModuleId = "msp-account-management-report",
    [string]$ExpectedModuleVersion = "0.1.3"
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

    if ($errorText -notlike "*already registered*") {
        try {
            if ($_.Exception.Response -and $_.Exception.Response.Content) {
                $responseBody = $_.Exception.Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                if (-not [string]::IsNullOrWhiteSpace($responseBody)) {
                    $errorText = $responseBody
                }
            }
            elseif ($_.Exception.Response -and $_.Exception.Response.GetResponseStream) {
                $stream = $_.Exception.Response.GetResponseStream()
                if ($stream) {
                    $reader = [System.IO.StreamReader]::new($stream)
                    $responseBody = $reader.ReadToEnd()
                    if (-not [string]::IsNullOrWhiteSpace($responseBody)) {
                        $errorText = $responseBody
                    }
                }
            }
        }
        catch {
            # Keep the first captured error text when PowerShell has already disposed the response body.
        }
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
    $_.manifest.id -eq $ExpectedModuleId -and
    $_.manifest.version -eq $ExpectedModuleVersion
} | Select-Object -First 1

if ($null -eq $match) {
    throw "Imported module was not found in module catalog."
}

Write-Host "Module catalog contains $ExpectedModuleId $ExpectedModuleVersion." -ForegroundColor Green
