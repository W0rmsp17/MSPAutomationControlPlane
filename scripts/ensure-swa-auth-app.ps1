[CmdletBinding()]
param(
    [string]$TerraformPath = "terraform",
    [string]$DisplayName = "MSP Automation Control Plane - Static Web App",
    [string[]]$AllowedUserObjectIds = @(),
    [string[]]$AllowedGroupIds = @(),
    [int]$SecretYears = 1
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$infraRoot = Join-Path $repoRoot "infra"

function Get-TerraformOutput {
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    Push-Location $infraRoot
    try {
        $value = & $TerraformPath output -raw $Name
        if ($LASTEXITCODE -ne 0) {
            throw "Terraform output '$Name' was not available. Run the infrastructure deployment first."
        }

        return $value.Trim()
    }
    finally {
        Pop-Location
    }
}

function Invoke-JsonCommand {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $json = & az @Arguments --output json
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed: az $($Arguments -join ' ')"
    }

    return $json | ConvertFrom-Json
}

$resourceGroupName = Get-TerraformOutput -Name "resource_group_name"
$mspTenantId = Get-TerraformOutput -Name "msp_tenant_id"
$functionAppName = Get-TerraformOutput -Name "function_app_name"
$staticWebAppName = Get-TerraformOutput -Name "static_web_app_name"
$staticWebAppHost = Get-TerraformOutput -Name "static_web_app_default_host_name"
$redirectUri = "https://$staticWebAppHost/.auth/login/aad/callback"
$spaRedirectUri = "https://$staticWebAppHost"

$existingApp = Invoke-JsonCommand -Arguments @(
    "ad",
    "app",
    "list",
    "--display-name",
    $DisplayName
) | Select-Object -First 1

if ($existingApp) {
    $app = $existingApp
    Write-Host "Using existing app registration: $($app.appId)"
}
else {
    $app = Invoke-JsonCommand -Arguments @(
        "ad",
        "app",
        "create",
        "--display-name",
        $DisplayName,
        "--sign-in-audience",
        "AzureADMyOrg",
        "--web-redirect-uris",
        $redirectUri
    )
    Write-Host "Created app registration: $($app.appId)"
}

$existingServicePrincipal = & az ad sp show --id $app.appId --output json 2>$null
if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($existingServicePrincipal)) {
    Write-Host "Using existing enterprise application/service principal: $($app.appId)"
}
else {
    Invoke-JsonCommand -Arguments @(
        "ad",
        "sp",
        "create",
        "--id",
        $app.appId
    ) | Out-Null
    Write-Host "Created enterprise application/service principal: $($app.appId)"
}

Invoke-JsonCommand -Arguments @(
    "ad",
    "app",
    "update",
    "--id",
    $app.appId,
    "--web-redirect-uris",
    $redirectUri
) | Out-Null

if ($AllowedUserObjectIds.Count -eq 0) {
    $signedInUser = Invoke-JsonCommand -Arguments @(
        "ad",
        "signed-in-user",
        "show"
    )
    $AllowedUserObjectIds = @($signedInUser.id)
}

$scope = $app.api.oauth2PermissionScopes | Where-Object { $_.value -eq "access_as_user" } | Select-Object -First 1
if (-not $scope) {
    $scope = [pscustomobject]@{
        id                      = [guid]::NewGuid().ToString()
        adminConsentDescription = "Allow the management UI to call the MSP Automation Control Plane API as the signed-in operator."
        adminConsentDisplayName = "Access MSP Automation Control Plane API"
        isEnabled               = $true
        type                    = "User"
        userConsentDescription  = "Call the MSP Automation Control Plane API as you."
        userConsentDisplayName  = "Access MSP Automation Control Plane"
        value                   = "access_as_user"
    }
}

$adminRole = $app.appRoles | Where-Object { $_.value -eq "ControlPlane.Admin" } | Select-Object -First 1
if (-not $adminRole) {
    $adminRole = [pscustomobject]@{
        allowedMemberTypes = @("User", "Application")
        description        = "Full administrative access to the MSP Automation Control Plane."
        displayName        = "Control Plane Admin"
        id                 = [guid]::NewGuid().ToString()
        isEnabled          = $true
        value              = "ControlPlane.Admin"
    }
}

$operatorRole = $app.appRoles | Where-Object { $_.value -eq "ControlPlane.Operator" } | Select-Object -First 1
if (-not $operatorRole) {
    $operatorRole = [pscustomobject]@{
        allowedMemberTypes = @("User")
        description        = "Operator access to the MSP Automation Control Plane."
        displayName        = "Control Plane Operator"
        id                 = [guid]::NewGuid().ToString()
        isEnabled          = $true
        value              = "ControlPlane.Operator"
    }
}

$appPatch = @{
    identifierUris = @("api://$($app.appId)")
    api            = @{
        oauth2PermissionScopes = @($scope)
    }
    appRoles       = @($adminRole, $operatorRole)
    spa            = @{
        redirectUris = @($spaRedirectUri)
    }
}

$appPatchJson = $appPatch | ConvertTo-Json -Depth 12 -Compress
$appPatchFile = New-TemporaryFile
try {
    Set-Content -LiteralPath $appPatchFile -Value $appPatchJson -Encoding utf8
    & az rest `
        --method PATCH `
        --uri "https://graph.microsoft.com/v1.0/applications/$($app.id)" `
        --headers "Content-Type=application/json" `
        --body "@$appPatchFile" `
        --output none

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to configure API scope, app roles, and SPA redirect URI on the app registration."
    }
}
finally {
    Remove-Item -LiteralPath $appPatchFile -Force -ErrorAction SilentlyContinue
}

$endDate = (Get-Date).AddYears($SecretYears).ToString("yyyy-MM-dd")
$secret = Invoke-JsonCommand -Arguments @(
    "ad",
    "app",
    "credential",
    "reset",
    "--id",
    $app.appId,
    "--display-name",
    "static-web-app-auth",
    "--end-date",
    $endDate
)

& az staticwebapp appsettings set `
    --name $staticWebAppName `
    --resource-group $resourceGroupName `
    --setting-names "AZURE_CLIENT_ID=$($app.appId)" "AZURE_CLIENT_SECRET=$($secret.password)" `
    --output none

if ($LASTEXITCODE -ne 0) {
    throw "Failed to update Static Web App authentication settings."
}

& az functionapp config appsettings set `
    --name $functionAppName `
    --resource-group $resourceGroupName `
    --settings `
        "ControlPlane__Auth__Enabled=true" `
        "ControlPlane__Auth__TenantId=$mspTenantId" `
        "ControlPlane__Auth__Audience=api://$($app.appId)" `
        "ControlPlane__Auth__RequiredScope=access_as_user" `
        "ControlPlane__Auth__AllowedUserObjectIds=$($AllowedUserObjectIds -join ',')" `
        "ControlPlane__Auth__AllowedGroupIds=$($AllowedGroupIds -join ',')" `
    --output none

if ($LASTEXITCODE -ne 0) {
    throw "Failed to update Function App authentication settings."
}

Write-Host "Static Web App authentication app settings updated." -ForegroundColor Green
Write-Host "Function App API token validation settings updated." -ForegroundColor Green
Write-Host "App registration client ID: $($app.appId)"
Write-Host "API scope: api://$($app.appId)/access_as_user"
Write-Host "Redirect URI: $redirectUri"
Write-Host "SPA redirect URI: $spaRedirectUri"
