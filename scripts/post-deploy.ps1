[CmdletBinding()]
param(
    [string]$TerraformPath = "terraform"
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$infraRoot = Join-Path $repoRoot "infra"

Push-Location $infraRoot
try {
    & $TerraformPath output
}
finally {
    Pop-Location
}
