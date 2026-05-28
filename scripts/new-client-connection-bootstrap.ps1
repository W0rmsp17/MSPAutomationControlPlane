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

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

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

function Get-GraphApplicationRoleId {
    param(
        [Parameter(Mandatory)]
        [string]$Permission
    )

    $knownGraphApplicationRoles = @{
        "Directory.Read.All"    = "7ab1d382-f21e-4acd-a863-ba3e13f7da61"
        "Organization.Read.All" = "498476ce-e0fe-48b0-b801-37ba7e2685c6"
        "User.Read.All"         = "df021288-bdef-4463-88db-98f22de89214"
    }

    if (-not $knownGraphApplicationRoles.ContainsKey($Permission)) {
        throw "Unknown Microsoft Graph application permission '$Permission'. Add its app role ID to scripts/new-client-connection-bootstrap.ps1 before using automated app registration mode."
    }

    return $knownGraphApplicationRoles[$Permission]
}

$executionAppClientId = $ExecutionAppClientId
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

    $graphResourceAccess = @($GraphApplicationPermissions | Sort-Object -Unique | ForEach-Object {
            [ordered]@{
                id   = Get-GraphApplicationRoleId -Permission $_
                type = "Role"
            }
        })

    $graphRequiredResource = [ordered]@{
        resourceAppId  = "00000003-0000-0000-c000-000000000000"
        resourceAccess = $graphResourceAccess
    }

    $requiredResourceAccessPath = Join-Path $repoRoot ".work/target-app/$ClientConnectionId-required-resource-access.json"
    $requiredResourceAccessParent = Split-Path -Parent $requiredResourceAccessPath
    if (-not (Test-Path $requiredResourceAccessParent)) {
        New-Item -ItemType Directory -Path $requiredResourceAccessParent | Out-Null
    }

    $graphRequiredResourceJson = $graphRequiredResource | ConvertTo-Json -Depth 8
    Set-Content -Path $requiredResourceAccessPath -Value "[$graphRequiredResourceJson]" -Encoding utf8

    az ad app update `
        --id $app.appId `
        --required-resource-accesses "@$requiredResourceAccessPath" `
        --output none

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to configure Microsoft Graph application permissions on target app registration '$($app.appId)'."
    }

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
    $notes = "Target app registration created with required Microsoft Graph application permissions. Add certificate credentials and grant/admin-consent required permissions before production use."
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

if ([string]::IsNullOrWhiteSpace($executionAppClientId)) {
    $executionAppClientId = "00000000-0000-0000-0000-000000000000"
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
        "Target tenant Azure subscription required: No. This bootstrap uses Entra ID app registration and service principal APIs.",
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
