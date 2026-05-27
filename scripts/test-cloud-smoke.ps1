[CmdletBinding()]
param(
    [string]$TerraformPath = "terraform",
    [string]$AuthAppDisplayName = "MSP Automation Control Plane - Static Web App",
    [string]$ModuleManifestPath = "modules/tenant-health-check/module.manifest.json",
    [string]$ClientConnectionPath = "samples/client-connection-contoso.json",
    [string]$JobRequestPath = "samples/submit-user-job.json",
    [switch]$RegistrationOnly,
    [switch]$AllowPlaceholderClient,
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

    try {
        return Invoke-RestMethod @arguments
    }
    catch {
        $errorText = $_.ErrorDetails.Message
        if ([string]::IsNullOrWhiteSpace($errorText)) {
            $errorText = $_.Exception.Message
        }

        try {
            $problem = $errorText | ConvertFrom-Json -ErrorAction Stop
            if ($problem.error) {
                $errorText = $problem.error
            }

            if ($problem.errors) {
                $errorText = "$errorText $($problem.errors -join ' ')"
            }
        }
        catch {
            # Keep the original error text when the response is not JSON.
        }

        throw "$Method $Path failed: $errorText"
    }
}

function Assert-ApiRecord {
    param(
        [object]$Value,

        [Parameter(Mandatory)]
        [string]$Operation,

        [Parameter(Mandatory)]
        [string]$RequiredProperty
    )

    if ($null -eq $Value) {
        throw "$Operation returned no response."
    }

    if ($Value.PSObject.Properties.Name -contains "error") {
        throw "$Operation failed: $($Value.error)"
    }

    if (-not ($Value.PSObject.Properties.Name -contains $RequiredProperty) -or
        [string]::IsNullOrWhiteSpace([string]$Value.$RequiredProperty)) {
        throw "$Operation returned an unexpected response. Expected property '$RequiredProperty'. Response: $($Value | ConvertTo-Json -Depth 8)"
    }
}

function Test-SmokeInputs {
    param(
        [Parameter(Mandatory)]
        [string]$ClientConnectionJson,

        [Parameter(Mandatory)]
        [string]$JobRequestJson
    )

    $client = $ClientConnectionJson | ConvertFrom-Json -ErrorAction Stop
    $job = $JobRequestJson | ConvertFrom-Json -ErrorAction Stop

    if ($job.clientConnectionId -ne $client.id) {
        throw "Job request clientConnectionId '$($job.clientConnectionId)' does not match client connection id '$($client.id)'."
    }

    $usesPlaceholderClient =
        $client.tenantId -eq "11111111-1111-1111-1111-111111111111" -or
        $client.executionAppClientId -eq "22222222-2222-2222-2222-222222222222" -or
        $client.certificateReference -eq "kv://certificates/client-contoso-graph"

    if ($usesPlaceholderClient -and -not $AllowPlaceholderClient -and -not $RegistrationOnly) {
        throw @"
The selected client connection uses public placeholder values and cannot run a live Container Apps job.

For a registration-only API check, rerun with:
  .\scripts\test-cloud-smoke.ps1 -RegistrationOnly

For a full execution smoke test, create untracked local JSON files with a real client connection and matching job request, then run:
  .\scripts\test-cloud-smoke.ps1 -ClientConnectionPath ".work\<client>.json" -JobRequestPath ".work\<job>.json"

The client connection certificateReference must point to a certificate that exists in the deployed Key Vault.
"@
    }
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

Test-SmokeInputs -ClientConnectionJson $clientConnection -JobRequestJson $jobRequest

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

if ($RegistrationOnly) {
    Write-Host "Registration-only cloud smoke completed. Job execution was skipped." -ForegroundColor Green
    return
}

$job = Invoke-ControlPlaneApi -Method "Post" -Path "jobs" -Body $jobRequest
Assert-ApiRecord -Value $job -Operation "Submit job" -RequiredProperty "id"
Write-Host "Job submitted: $($job.id) [$($job.status)]"

Start-Sleep -Seconds $DispatchWaitSeconds

$job = Invoke-ControlPlaneApi -Method "Get" -Path "jobs/$($job.id)"
Assert-ApiRecord -Value $job -Operation "Load job" -RequiredProperty "id"
Write-Host "Job after dispatch wait: $($job.id) [$($job.status)]"

$collected = $null
for ($attempt = 1; $attempt -le $CollectAttempts; $attempt++) {
    try {
        $collected = Invoke-ControlPlaneApi -Method "Post" -Path "jobs/$($job.id)/collect-result"
        Assert-ApiRecord -Value $collected -Operation "Collect job result" -RequiredProperty "id"
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
