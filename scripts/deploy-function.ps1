[CmdletBinding()]
param(
    [string]$TerraformPath = "terraform",
    [string]$Configuration = "Release",
    [string]$ProjectPath = "MSPAutomationControlPlane\MSPAutomationControlPlane.csproj",
    [string]$PackageRoot = ".deploy",
    [switch]$SkipBuild
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$infraRoot = Join-Path $repoRoot "infra"
$resolvedProjectPath = Join-Path $repoRoot $ProjectPath
$packageRootPath = Join-Path $repoRoot $PackageRoot
$publishPath = Join-Path $packageRootPath "function-publish"
$packagePath = Join-Path $packageRootPath "control-api.zip"

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
$functionAppName = Get-TerraformOutput -Name "function_app_name"

if (-not (Test-Path $packageRootPath)) {
    New-Item -ItemType Directory -Path $packageRootPath | Out-Null
}

if (Test-Path $publishPath) {
    Remove-Item -LiteralPath $publishPath -Recurse -Force
}

if (Test-Path $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

if (-not $SkipBuild) {
    Invoke-CheckedCommand -FilePath "dotnet" -Arguments @(
        "publish",
        $resolvedProjectPath,
        "--configuration",
        $Configuration,
        "--output",
        $publishPath
    )
}

Compress-Archive -Path (Join-Path $publishPath "*") -DestinationPath $packagePath -Force

Invoke-CheckedCommand -FilePath "az" -Arguments @(
    "functionapp",
    "deployment",
    "source",
    "config-zip",
    "--resource-group",
    $resourceGroupName,
    "--name",
    $functionAppName,
    "--src",
    $packagePath
)

Write-Host "Function App deployed: $functionAppName" -ForegroundColor Green
Write-Host "Package: $packagePath"
