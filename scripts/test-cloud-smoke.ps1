[CmdletBinding()]
param(
    [string]$TerraformPath = "terraform",
    [string]$AuthAppDisplayName = "MSP Automation Control Plane - Static Web App",
    [string]$ModuleManifestPath = "modules/tenant-health-check/module.manifest.json",
    [string]$ClientConnectionPath = "samples/client-connection-contoso.json",
    [string]$JobRequestPath = "samples/submit-user-job.json",
    [int]$DispatchWaitSeconds = 40,
    [int]$CollectAttempts = 6,
    [int]$CollectWaitSeconds = 20
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$infraRoot = Join-Path $repoRoot "infra"

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

function Get-RequiredFileContent {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $resolvedPath = Resolve-Path (Join-Path $repoRoot $Path) -ErrorAction Stop
    return Get-Content -LiteralPath $resolvedPath -Raw
}

function Invoke-ControlPlaneApi {
    param(
        [Parameter(Mandatory)]
        [string]$Method,

        [Parameter(Mandatory)]
        [string]$Path,

        [object]$Body = $null
    )

    $uri = "$($script:ApiBaseUrl.TrimEnd('/'))/$($Path.TrimStart('/'))"
    $arguments = @{
        Uri         = $uri
        Method      = $Method
        Headers     = $script:Headers
        ErrorAction = "Stop"
    }

    if ($null -ne $Body) {
        $arguments.ContentType = "application/json"
        $arguments.Body = $Body
    }

    return Invoke-RestMethod @arguments
}

function Invoke-RegistrationOrReuse {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Body,

        [Parameter(Mandatory)]
        [string]$SuccessMessage,

        [Parameter(Mandatory)]
        [string]$AlreadyRegisteredMessage
    )

    try {
        Invoke-ControlPlaneApi -Method "Post" -Path $Path -Body $Body | Out-Null
        Write-Host $SuccessMessage
    }
    catch {
        $errorText = $_.ErrorDetails.Message
        if ([string]::IsNullOrWhiteSpace($errorText)) {
            $errorText = $_.Exception.Message
        }

        if ($errorText -like "*already registered*") {
            Write-Host $AlreadyRegisteredMessage
            return
        }

        throw
    }
}

$mspTenantId = Get-TerraformOutput -Name "msp_tenant_id"
$functionHostName = Get-TerraformOutput -Name "function_app_default_hostname"
$script:ApiBaseUrl = "https://$functionHostName/api"

$authApp = az ad app list `
    --display-name $AuthAppDisplayName `
    --query "[0]" `
    --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0 -or $null -eq $authApp -or [string]::IsNullOrWhiteSpace($authApp.appId)) {
    throw "Could not find the Static Web App/API auth app registration. Run scripts\ensure-swa-auth-app.ps1 first."
}

$apiScope = "api://$($authApp.appId)/access_as_user"
$accessToken = az account get-access-token `
    --tenant $mspTenantId `
    --scope $apiScope `
    --query accessToken `
    --output tsv

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($accessToken)) {
    throw "Could not acquire an access token for '$apiScope'. Run: az login --tenant `"$mspTenantId`" --scope `"$apiScope`""
}

$script:Headers = @{ Authorization = "Bearer $accessToken" }

Write-Host "API base URL: $script:ApiBaseUrl"
Write-Host "API scope:    $apiScope"

$unauthenticatedRejected = $false
try {
    Invoke-RestMethod -Uri "$script:ApiBaseUrl/jobs" -Method Get -ErrorAction Stop | Out-Null
}
catch {
    $unauthenticatedRejected = $true
    Write-Host "Unauthenticated API check rejected as expected."
}

if (-not $unauthenticatedRejected) {
    throw "Unauthenticated API check unexpectedly succeeded."
}

$moduleManifest = Get-RequiredFileContent -Path $ModuleManifestPath
$clientConnection = Get-RequiredFileContent -Path $ClientConnectionPath
$jobRequest = Get-RequiredFileContent -Path $JobRequestPath

Invoke-RegistrationOrReuse `
    -Path "modules" `
    -Body $moduleManifest `
    -SuccessMessage "Module registered." `
    -AlreadyRegisteredMessage "Module already registered; reusing existing record."

Invoke-RegistrationOrReuse `
    -Path "client-connections" `
    -Body $clientConnection `
    -SuccessMessage "Client connection registered." `
    -AlreadyRegisteredMessage "Client connection already registered; reusing existing record."

$job = Invoke-ControlPlaneApi -Method "Post" -Path "jobs" -Body $jobRequest
Write-Host "Job submitted: $($job.id) [$($job.status)]"

Start-Sleep -Seconds $DispatchWaitSeconds

$job = Invoke-ControlPlaneApi -Method "Get" -Path "jobs/$($job.id)"
Write-Host "Job after dispatch wait: $($job.id) [$($job.status)]"

$collected = $null
for ($attempt = 1; $attempt -le $CollectAttempts; $attempt++) {
    try {
        $collected = Invoke-ControlPlaneApi -Method "Post" -Path "jobs/$($job.id)/collect-result"
        break
    }
    catch {
        if ($attempt -eq $CollectAttempts) {
            throw
        }

        Write-Host "Result not available yet. Waiting $CollectWaitSeconds seconds before collect attempt $($attempt + 1)."
        Start-Sleep -Seconds $CollectWaitSeconds
    }
}

if ($null -eq $collected -or $collected.status -ne "Succeeded") {
    throw "Smoke test failed. Expected Succeeded, received '$($collected.status)'."
}

Write-Host "Job completed: $($collected.id) [$($collected.status)]" -ForegroundColor Green
Write-Host "Summary: $($collected.output.summary)"
