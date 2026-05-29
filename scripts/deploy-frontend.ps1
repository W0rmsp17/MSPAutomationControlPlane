[CmdletBinding()]
param(
    [string]$TerraformPath = "terraform",
    [string]$SourcePath = "frontend",
    [string]$PackageRoot = ".deploy",
    [string]$AuthAppDisplayName = "MSP Automation Control Plane - Static Web App",
    [string]$StaticWebAppsCliPackage = "@azure/static-web-apps-cli@2.0.8",
    [string]$StaticSitesClientPath,
    [switch]$SkipDeploy
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$infraRoot = Join-Path $repoRoot "infra"
$sourceRoot = Join-Path $repoRoot $SourcePath
$packageRootPath = Join-Path $repoRoot $PackageRoot
$publishPath = Join-Path $packageRootPath "static-web-app"

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [string]$SafeDescription
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        if ([string]::IsNullOrWhiteSpace($SafeDescription)) {
            $SafeDescription = $FilePath
        }

        throw "Command failed: $SafeDescription"
    }
}

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

function Set-Utf8NoBomContent {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Value
    )

    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Value, $utf8NoBom)
}

$resourceGroupName = Get-TerraformOutput -Name "resource_group_name"
$mspTenantId = Get-TerraformOutput -Name "msp_tenant_id"
$functionHostName = Get-TerraformOutput -Name "function_app_default_hostname"
$staticWebAppName = Get-TerraformOutput -Name "static_web_app_name"
$apiBaseUrl = "https://$functionHostName/api"

if (-not (Test-Path $packageRootPath)) {
    New-Item -ItemType Directory -Path $packageRootPath | Out-Null
}

if (Test-Path $publishPath) {
    Remove-Item -LiteralPath $publishPath -Recurse -Force
}

Copy-Item -Path $sourceRoot -Destination $publishPath -Recurse

$configPath = Join-Path $publishPath "app-config.js"
$assetVersion = Get-Date -Format "yyyyMMddHHmmss"
$authApp = az ad app list `
    --display-name $AuthAppDisplayName `
    --query "[0]" `
    --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0 -or $null -eq $authApp -or [string]::IsNullOrWhiteSpace($authApp.appId)) {
    throw "Could not find the Static Web App/API auth app registration. Run scripts\ensure-swa-auth-app.ps1 first."
}

$apiScope = "api://$($authApp.appId)/access_as_user"
@"
window.MSP_CONTROL_PLANE_CONFIG = {
  apiBaseUrl: "$apiBaseUrl",
  auth: {
    tenantId: "$mspTenantId",
    clientId: "$($authApp.appId)",
    apiScope: "$apiScope"
  }
};
"@ | ForEach-Object { Set-Utf8NoBomContent -Path $configPath -Value $_ }

$versionedAppPath = Join-Path $publishPath "app.$assetVersion.js"
$versionedConfigPath = Join-Path $publishPath "app-config.$assetVersion.js"
Move-Item -LiteralPath (Join-Path $publishPath "app.js") -Destination $versionedAppPath -Force
Move-Item -LiteralPath $configPath -Destination $versionedConfigPath -Force

$indexPath = Join-Path $publishPath "index.html"
$indexContent = Get-Content -LiteralPath $indexPath -Raw
$indexContent = $indexContent.
    Replace("./styles.css", "./styles.css?v=$assetVersion").
    Replace("./app-config.js", "./app-config.$assetVersion.js").
    Replace("./app.js", "./app.$assetVersion.js")
Set-Utf8NoBomContent -Path $indexPath -Value $indexContent

$authTemplatePath = Join-Path $publishPath "staticwebapp.config.template.json"
$authConfigPath = Join-Path $publishPath "staticwebapp.config.json"
if (Test-Path $authTemplatePath) {
    $authConfigContent = (Get-Content $authTemplatePath -Raw).Replace("__MSP_TENANT_ID__", $mspTenantId)
    Set-Utf8NoBomContent -Path $authConfigPath -Value $authConfigContent
    Remove-Item -LiteralPath $authTemplatePath -Force
}

if ($SkipDeploy) {
    Write-Host "Static Web App package prepared: $publishPath" -ForegroundColor Green
    return
}

$deploymentToken = az staticwebapp secrets list `
    --name $staticWebAppName `
    --resource-group $resourceGroupName `
    --query "properties.apiKey" `
    --output tsv

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($deploymentToken)) {
    throw "Could not read the Static Web App deployment token."
}

if (-not [string]::IsNullOrWhiteSpace($StaticSitesClientPath)) {
    Invoke-CheckedCommand -FilePath $StaticSitesClientPath -Arguments @(
        "upload",
        "--app",
        $publishPath,
        "--outputLocation",
        ".",
        "--appArtifactLocation",
        ".",
        "--skipAppBuild",
        "true",
        "--apiToken",
        $deploymentToken,
        "--deploymentProvider",
        "SwaCli"
    ) -SafeDescription "StaticSitesClient upload --app <publishPath> --apiToken <redacted>"
}
else {
    Invoke-CheckedCommand -FilePath "npx" -Arguments @(
    "--yes",
    $StaticWebAppsCliPackage,
    "deploy",
    $publishPath,
    "--deployment-token",
    $deploymentToken,
    "--env",
    "production"
) -SafeDescription "npx $StaticWebAppsCliPackage deploy <publishPath> --deployment-token <redacted> --env production"
}

Write-Host "Static Web App deployed: $staticWebAppName" -ForegroundColor Green
Write-Host "API base URL: $apiBaseUrl"
