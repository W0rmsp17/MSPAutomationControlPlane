# Local Development

The first implementation slice runs locally with in-memory repositories and a stub operator identity.

Local mode proves the control plane flow before Azure Table Storage, Service Bus, Key Vault, and Container Apps Jobs are wired in.

## Run The Function App

Create a local settings file from the sample:

```powershell
Copy-Item .\MSPAutomationControlPlane\local.settings.sample.json .\MSPAutomationControlPlane\local.settings.json
```

Start the Functions host from the project folder:

```powershell
cd .\MSPAutomationControlPlane
func start --port 7071
```

The sample local settings use `UseDevelopmentStorage=true`. If Azurite is not running, the Functions host may report the storage health check as unhealthy. The current in-memory MVP endpoints still run, but Azurite should be started once storage-triggered functions or local Azure Storage testing are added.

Local development defaults to in-memory repositories:

```json
"ControlPlane__RepositoryProvider": "InMemory"
```

To test Azure Table Storage adapters locally, start Azurite and set:

```json
"ControlPlane__RepositoryProvider": "TableStorage",
"ControlPlane__StorageConnectionString": "UseDevelopmentStorage=true"
```

The current local endpoints are:

```text
GET  /api/health
GET  /api/audit-events
POST /api/client-connections
GET  /api/client-connections
POST /api/modules
GET  /api/modules
POST /api/notification-subscriptions
GET  /api/notification-subscriptions
DELETE /api/notification-subscriptions/{id}
POST /api/jobs
GET  /api/jobs/{id}
```

## Smoke Test

From the repository root:

```powershell
Invoke-RestMethod -Uri 'http://localhost:7071/api/health' -Method Get
```

Register the sample module:

```powershell
$manifest = Get-Content .\samples\tenant-health-check.manifest.json -Raw
Invoke-RestMethod -Uri 'http://localhost:7071/api/modules' -Method Post -ContentType 'application/json' -Body $manifest
```

Register the sample client connection:

```powershell
$client = Get-Content .\samples\client-connection-contoso.json -Raw
Invoke-RestMethod -Uri 'http://localhost:7071/api/client-connections' -Method Post -ContentType 'application/json' -Body $client
```

List modules:

```powershell
Invoke-RestMethod -Uri 'http://localhost:7071/api/modules' -Method Get
```

Register the sample notification subscription:

```powershell
$notification = Get-Content .\samples\notification-subscription-teams.json -Raw
Invoke-RestMethod -Uri 'http://localhost:7071/api/notification-subscriptions' -Method Post -ContentType 'application/json' -Body $notification
```

Submit a sample user-scoped job:

```powershell
$job = Get-Content .\samples\submit-user-job.json -Raw
Invoke-RestMethod -Uri 'http://localhost:7071/api/jobs' -Method Post -ContentType 'application/json' -Body $job
```

Read job status:

```powershell
Invoke-RestMethod -Uri 'http://localhost:7071/api/jobs/{jobId}' -Method Get
```

List audit events:

```powershell
Invoke-RestMethod -Uri 'http://localhost:7071/api/audit-events' -Method Get
```

## Current MVP Limits

- Data is stored in memory and disappears when the Function App restarts.
- Operator identity is stubbed as `operator@local.dev`.
- Job dispatch is simulated.
- Service Bus is not wired yet.
- Container Apps Jobs are not wired yet.
- Key Vault is not wired yet.
