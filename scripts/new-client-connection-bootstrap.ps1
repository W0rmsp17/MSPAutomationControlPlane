[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ClientConnectionId,

    [Parameter(Mandatory)]
    [string]$DisplayName,

    [Parameter(Mandatory)]
    [string]$TenantId,

    [string]$AppDisplayName,

    [string]$CertificateReference,

    [string[]]$EnabledModuleIds = @("tenant-health-check"),

    [string[]]$AllowedScopes = @("Tenant", "Users"),

    [string[]]$GraphApplicationPermissions = @("Organization.Read.All"),

    [switch]$CreateAppRegistration,

    [switch]$AdminConsented,

    [string]$OutputPath
)

if ([string]::IsNullOrWhiteSpace($AppDisplayName)) {
    $AppDisplayName = "MSP Control Plane - $DisplayName"
}

if ([string]::IsNullOrWhiteSpace($CertificateReference)) {
    $CertificateReference = "kv://clients/$ClientConnectionId/graph-certificate"
}

function Invoke-AzJson {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $json = & az @Arguments --output json
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed: az $($Arguments -join ' ')"
    }

    if ([string]::IsNullOrWhiteSpace($json)) {
        return $null
    }

    return $json | ConvertFrom-Json
}

$executionAppClientId = "00000000-0000-0000-0000-000000000000"
$servicePrincipalObjectId = $null
$readinessStatus = "Draft"
$notes = "Manual target app registration values must be supplied before this connection is ready."

if ($CreateAppRegistration) {
    $app = Invoke-AzJson -Arguments @(
        "ad",
        "app",
        "create",
        "--display-name",
        $AppDisplayName,
        "--sign-in-audience",
        "AzureADMyOrg"
    )

    $sp = Invoke-AzJson -Arguments @(
        "ad",
        "sp",
        "create",
        "--id",
        $app.appId
    )

    $executionAppClientId = $app.appId
    $servicePrincipalObjectId = $sp.id
    $readinessStatus = if ($AdminConsented) { "Ready" } else { "PendingConsent" }
    $notes = "Target app registration created. Add certificate credentials and grant/admin-consent required permissions before production use."
}

$configuredPermissions = foreach ($permission in $GraphApplicationPermissions) {
    [ordered]@{
        provider       = "MicrosoftGraph"
        permission     = $permission
        type           = "Application"
        adminConsented = [bool]$AdminConsented
    }
}

$connection = [ordered]@{
    id                       = $ClientConnectionId
    displayName              = $DisplayName
    tenantId                 = $TenantId
    executionMode            = "Central"
    executionAppClientId     = $executionAppClientId
    certificateReference     = $CertificateReference
    servicePrincipalObjectId = $servicePrincipalObjectId
    readinessStatus          = $readinessStatus
    configuredPermissions    = @($configuredPermissions)
    lastReadinessCheckAt     = (Get-Date).ToUniversalTime().ToString("o")
    readinessNotes           = $notes
    enabledModuleIds         = @($EnabledModuleIds)
    allowedScopes            = @($AllowedScopes)
    enabled                  = $true
}

$jsonOutput = $connection | ConvertTo-Json -Depth 8

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $resolvedOutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
    $parent = Split-Path -Parent $resolvedOutputPath
    if (-not [string]::IsNullOrWhiteSpace($parent) -and -not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }

    Set-Content -Path $resolvedOutputPath -Value $jsonOutput -Encoding utf8
    Write-Host "Client connection written: $resolvedOutputPath" -ForegroundColor Green
}
else {
    $jsonOutput
}
