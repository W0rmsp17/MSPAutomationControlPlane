[CmdletBinding()]
param()

$account = az account show --output json | ConvertFrom-Json

[pscustomobject]@{
    SubscriptionId   = $account.id
    SubscriptionName = $account.name
    TenantId         = $account.tenantId
    User             = $account.user.name
} | Format-List

Write-Host ""
Write-Host "Use the SubscriptionId and TenantId values in infra/environments/<env>/terraform.tfvars." -ForegroundColor Cyan
