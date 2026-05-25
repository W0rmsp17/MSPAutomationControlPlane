[CmdletBinding()]
param(
    [string]$TerraformPath = "terraform"
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$infraRoot = Join-Path $repoRoot "infra"

function Get-TerraformOutput {
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    $value = & $TerraformPath output -raw $Name 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return $value.Trim()
}

Push-Location $infraRoot
try {
    $resourceGroupName = Get-TerraformOutput -Name "resource_group_name"
    $functionAppName = Get-TerraformOutput -Name "function_app_name"
    $functionHostName = Get-TerraformOutput -Name "function_app_default_hostname"
    $staticWebAppHostName = Get-TerraformOutput -Name "static_web_app_default_host_name"
    $serviceBusNamespaceName = Get-TerraformOutput -Name "service_bus_namespace_name"
    $jobQueueName = Get-TerraformOutput -Name "job_queue_name"
    $keyVaultUri = Get-TerraformOutput -Name "key_vault_uri"
    $containerAppEnvironmentName = Get-TerraformOutput -Name "container_app_environment_name"

    Write-Host "MSP Automation Control Plane deployment" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Resource group:        $resourceGroupName"
    Write-Host "Function App:          $functionAppName"
    Write-Host "Function API base URL: https://$functionHostName/api"
    Write-Host "Static Web App:        https://$staticWebAppHostName"
    Write-Host "Service Bus namespace: $serviceBusNamespaceName"
    Write-Host "Job queue:             $jobQueueName"
    Write-Host "Key Vault:             $keyVaultUri"
    Write-Host "Container Apps env:    $containerAppEnvironmentName"
    Write-Host ""
    Write-Host "Health endpoint:       https://$functionHostName/api/health"
    Write-Host ""
    Write-Host "Next step: run scripts\deploy-function.ps1 to publish the Function App code."
}
finally {
    Pop-Location
}
