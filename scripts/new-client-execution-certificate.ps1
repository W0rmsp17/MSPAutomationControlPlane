[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ClientConnectionId,

    [Parameter(Mandatory)]
    [string]$TargetTenantId,

    [Parameter(Mandatory)]
    [string]$ExecutionAppClientId,

    [Parameter(Mandatory)]
    [string]$KeyVaultName,

    [string]$CertificateName,

    [int]$ValidYears = 1,

    [string]$OutputDirectory = ".work\client-certificates",

    [switch]$SkipTargetAppUpdate,

    [switch]$SkipKeyVaultImport
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($CertificateName)) {
    $CertificateName = "$ClientConnectionId-graph"
}

if ($ValidYears -lt 1) {
    throw "ValidYears must be at least 1."
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

$resolvedOutputDirectory = Resolve-Path $OutputDirectory
$safeName = $CertificateName -replace "[^A-Za-z0-9.-]", "-"
$pfxPath = Join-Path $resolvedOutputDirectory "$safeName.pfx"
$cerPath = Join-Path $resolvedOutputDirectory "$safeName.cer"
$passwordPath = Join-Path $resolvedOutputDirectory "$safeName.password.txt"

$password = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$securePassword = ConvertTo-SecureString -String $password -AsPlainText -Force
$notAfter = (Get-Date).ToUniversalTime().AddYears($ValidYears)

$certificate = New-SelfSignedCertificate `
    -Subject "CN=$CertificateName" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -NotAfter $notAfter `
    -HashAlgorithm SHA256

try {
    Export-PfxCertificate `
        -Cert $certificate `
        -FilePath $pfxPath `
        -Password $securePassword | Out-Null

    Export-Certificate `
        -Cert $certificate `
        -FilePath $cerPath | Out-Null

    Set-Content -LiteralPath $passwordPath -Value $password -Encoding utf8

    if (-not $SkipTargetAppUpdate) {
        az ad app credential reset `
            --id $ExecutionAppClientId `
            --tenant $TargetTenantId `
            --cert "@$cerPath" `
            --append `
            --display-name $CertificateName `
            --end-date $notAfter.ToString("yyyy-MM-dd") `
            --output none

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to add public certificate credential to target app registration."
        }
    }

    if (-not $SkipKeyVaultImport) {
        az keyvault certificate import `
            --vault-name $KeyVaultName `
            --name $CertificateName `
            --file $pfxPath `
            --password $password `
            --output none

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to import PFX certificate into Key Vault."
        }
    }

    [ordered]@{
        clientConnectionId    = $ClientConnectionId
        targetTenantId        = $TargetTenantId
        executionAppClientId  = $ExecutionAppClientId
        certificateName       = $CertificateName
        certificateReference  = "kv://certificates/$CertificateName"
        keyVaultName          = $KeyVaultName
        publicCertificatePath = $cerPath
        pfxPath               = $pfxPath
        passwordPath          = $passwordPath
        notAfter              = $notAfter.ToString("o")
    } | ConvertTo-Json -Depth 4
}
finally {
    if ($certificate) {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }
}
