[CmdletBinding()]
param(
    [string]$Environment = "cholbing-dev",
    [string]$TerraformPath = "terraform",
    [switch]$Apply,
    [switch]$AutoApprove
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

Push-Location $infraRoot
try {
    Invoke-Terraform -Arguments @("init")
    Invoke-Terraform -Arguments @("validate")

    if ($Apply) {
        $applyArguments = @("apply", "-var-file=$tfvarsPath")
        if ($AutoApprove) {
            $applyArguments += "-auto-approve"
        }

        Invoke-Terraform -Arguments $applyArguments
    }
    else {
        Invoke-Terraform -Arguments @("plan", "-var-file=$tfvarsPath")
    }
}
finally {
    Pop-Location
}
