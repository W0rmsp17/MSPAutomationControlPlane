[CmdletBinding()]
param(
    [string]$TerraformPath = "terraform",
    [string]$SourcePath = "frontend",
    [string]$PackageRoot = ".deploy",
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
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $FilePath $($Arguments -join ' ')"
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

$resourceGroupName = Get-TerraformOutput -Name "resource_group_name"
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
@"
window.MSP_CONTROL_PLANE_CONFIG = {
  apiBaseUrl: "$apiBaseUrl"
};
"@ | Set-Content -Path $configPath -Encoding utf8

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

Invoke-CheckedCommand -FilePath "npx" -Arguments @(
    "--yes",
    "@azure/static-web-apps-cli",
    "deploy",
    $publishPath,
    "--deployment-token",
    $deploymentToken,
    "--env",
    "production"
)

Write-Host "Static Web App deployed: $staticWebAppName" -ForegroundColor Green
Write-Host "API base URL: $apiBaseUrl"
