[CmdletBinding()]
param(
    [string]$TerraformPath = "terraform",
    [string]$DisplayName = "MSP Automation Control Plane - Static Web App",
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
$staticWebAppName = Get-TerraformOutput -Name "static_web_app_name"
$staticWebAppHost = Get-TerraformOutput -Name "static_web_app_default_host_name"
$redirectUri = "https://$staticWebAppHost/.auth/login/aad/callback"

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

Invoke-JsonCommand -Arguments @(
    "ad",
    "app",
    "update",
    "--id",
    $app.appId,
    "--web-redirect-uris",
    $redirectUri
) | Out-Null

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

Write-Host "Static Web App authentication app settings updated." -ForegroundColor Green
Write-Host "App registration client ID: $($app.appId)"
Write-Host "Redirect URI: $redirectUri"
