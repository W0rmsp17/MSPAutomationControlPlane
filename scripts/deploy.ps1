[CmdletBinding()]
param(
    [string]$Environment = "plutonix-dev",
    [string]$TerraformPath = "terraform",
    [switch]$Apply
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$infraRoot = Join-Path $repoRoot "infra"
$environmentPath = Join-Path $infraRoot "environments\$Environment"
$tfvarsPath = Join-Path $environmentPath "terraform.tfvars"

if (-not (Test-Path $tfvarsPath)) {
    throw "Missing tfvars file: $tfvarsPath. Copy terraform.tfvars.example to terraform.tfvars and set the MSP subscription and tenant values."
}

Push-Location $infraRoot
try {
    & $TerraformPath init
    & $TerraformPath validate

    if ($Apply) {
        & $TerraformPath apply -var-file="$tfvarsPath"
    }
    else {
        & $TerraformPath plan -var-file="$tfvarsPath"
    }
}
finally {
    Pop-Location
}
