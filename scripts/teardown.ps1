[CmdletBinding()]
param(
    [string]$Environment = "cholbing-dev",
    [string]$TerraformPath = "terraform",
    [switch]$Destroy,
    [switch]$AutoApprove,
    [switch]$KeepAuthApp,
    [string]$AuthAppClientId = "",
    [string]$AuthAppDisplayName = "MSP Automation Control Plane - Static Web App"
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$infraRoot = Join-Path $repoRoot "infra"
$environmentPath = Join-Path $infraRoot "environments\$Environment"
$tfvarsPath = Join-Path $environmentPath "terraform.tfvars"

if (-not (Test-Path $tfvarsPath)) {
    throw "Missing tfvars file: $tfvarsPath. Copy terraform.tfvars.example to terraform.tfvars and set the MSP subscription and tenant values."
}

function Invoke-Terraform {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & $TerraformPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Terraform command failed: $TerraformPath $($Arguments -join ' ')"
    }
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

function Get-AuthApp {
    if (-not [string]::IsNullOrWhiteSpace($AuthAppClientId)) {
        try {
            return Invoke-AzJson -Arguments @("ad", "app", "show", "--id", $AuthAppClientId)
        }
        catch {
            Write-Warning "Auth app '$AuthAppClientId' was not found."
            return $null
        }
    }

    $apps = @(Invoke-AzJson -Arguments @("ad", "app", "list", "--display-name", $AuthAppDisplayName))
    if ($apps.Count -eq 0) {
        Write-Warning "No auth app registration found with display name '$AuthAppDisplayName'."
        return $null
    }

    if ($apps.Count -gt 1) {
        $appIds = $apps | ForEach-Object { $_.appId }
        throw "Multiple auth app registrations matched '$AuthAppDisplayName': $($appIds -join ', '). Re-run with -AuthAppClientId."
    }

    return $apps[0]
}

function Remove-AuthApp {
    if ($KeepAuthApp) {
        Write-Host "Skipping auth app registration deletion because -KeepAuthApp was supplied." -ForegroundColor Yellow
        return
    }

    $app = Get-AuthApp
    if ($null -eq $app) {
        return
    }

    if (-not $Destroy) {
        Write-Host "Auth app registration that would be deleted: $($app.displayName) ($($app.appId))" -ForegroundColor Yellow
        return
    }

    & az ad app delete --id $app.appId --output none
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to delete auth app registration '$($app.appId)'."
    }

    Write-Host "Deleted auth app registration: $($app.displayName) ($($app.appId))" -ForegroundColor Green
}

Push-Location $infraRoot
try {
    Invoke-Terraform -Arguments @("init")
    Invoke-Terraform -Arguments @("validate")

    if ($Destroy) {
        $destroyArguments = @("destroy", "-var-file=$tfvarsPath")
        if ($AutoApprove) {
            $destroyArguments += "-auto-approve"
        }

        Invoke-Terraform -Arguments $destroyArguments
    }
    else {
        Invoke-Terraform -Arguments @("plan", "-destroy", "-var-file=$tfvarsPath")
    }
}
finally {
    Pop-Location
}

Remove-AuthApp

if (-not $Destroy) {
    Write-Host ""
    Write-Host "Dry run complete. Re-run with -Destroy to remove Terraform resources and the script-managed auth app registration." -ForegroundColor Cyan
}
