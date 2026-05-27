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

    [string]$ExecutionAppClientId,

    [string]$ServicePrincipalObjectId,

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
    $CertificateReference = "kv://certificates/$ClientConnectionId-graph"
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

$executionAppClientId = if ([string]::IsNullOrWhiteSpace($ExecutionAppClientId)) { "00000000-0000-0000-0000-000000000000" } else { $ExecutionAppClientId }
$servicePrincipalObjectId = if ([string]::IsNullOrWhiteSpace($ServicePrincipalObjectId)) { $null } else { $ServicePrincipalObjectId }
$readinessStatus = "Draft"
$notes = "Manual target app registration values must be supplied before this connection is ready."

if ($CreateAppRegistration) {
    if (-not [string]::IsNullOrWhiteSpace($ExecutionAppClientId) -or -not [string]::IsNullOrWhiteSpace($ServicePrincipalObjectId)) {
        throw "Do not pass -ExecutionAppClientId or -ServicePrincipalObjectId with -CreateAppRegistration."
    }

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
elseif (-not [string]::IsNullOrWhiteSpace($ExecutionAppClientId) -or -not [string]::IsNullOrWhiteSpace($ServicePrincipalObjectId)) {
    $readinessStatus = if ($AdminConsented) { "Ready" } else { "PendingConsent" }
    $notes = if ($AdminConsented) {
        "Manual target app registration metadata supplied and permissions marked admin-consented."
    }
    else {
        "Manual target app registration metadata supplied. Grant/admin-consent required permissions before marking Ready."
    }
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

    $notesPath = [System.IO.Path]::ChangeExtension($resolvedOutputPath, ".next-steps.md")
    $nextSteps = @(
        "# Client Connection Next Steps",
        "",
        "Client: $DisplayName",
        "Connection ID: $ClientConnectionId",
        "Tenant ID: $TenantId",
        "Readiness: $readinessStatus",
        "",
        "1. Register or update the generated connection JSON in the MSP control plane.",
        "2. Confirm the target tenant app registration and service principal IDs are correct.",
        "3. Add a certificate credential to the target app registration.",
        "4. Store the certificate private key in MSP Key Vault as a certificate matching: $CertificateReference",
        "5. Grant and admin-consent the required Microsoft Graph application permissions:",
        ($GraphApplicationPermissions | ForEach-Object { "   - $_" }),
        "6. Update the client connection readiness to Ready after permissions and certificate access are confirmed.",
        "7. Run the control plane readiness check before submitting module jobs."
    )
    Set-Content -Path $notesPath -Value $nextSteps -Encoding utf8
    Write-Host "Next steps written: $notesPath" -ForegroundColor Green
}
else {
    $jsonOutput
}
