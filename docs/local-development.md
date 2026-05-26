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

Local development also keeps API authentication disabled unless explicitly enabled:

```json
"ControlPlane__Auth__Enabled": "false"
```

Deployed environments enable API authentication through `ensure-swa-auth-app.ps1`. If you want to test token validation locally, configure:

```json
"ControlPlane__Auth__Enabled": "true",
"ControlPlane__Auth__TenantId": "<msp-tenant-id>",
"ControlPlane__Auth__Audience": "api://<app-client-id>",
"ControlPlane__Auth__RequiredScope": "access_as_user",
"ControlPlane__Auth__AllowedUserObjectIds": "<operator-object-id>"
```

To test Azure Table Storage adapters locally, start Azurite and set:

```json
"ControlPlane__RepositoryProvider": "TableStorage",
"ControlPlane__StorageConnectionString": "UseDevelopmentStorage=true"
```

Local development also defaults to the in-memory queue:

```json
"ControlPlane__QueueProvider": "InMemory"
```

Local development uses the local-or-simulated execution provider:

```json
"ControlPlane__ExecutionProvider": "LocalOrSimulated"
```

The deployed Terraform stack also defaults to `LocalOrSimulated` while the Container Apps result collection loop is being built. To test ARM-based execution later, switch the Terraform `execution_provider` variable to `ContainerApps`. In that mode, the Function App uses its managed identity to start the reusable Container Apps Job created by Terraform. The current Container Apps slice starts an execution, passes the standard job contract as `CONTROL_PLANE_JOB_INPUT_BASE64`, and leaves the job in `Running`; a polling/result-collection slice will complete the job after module output is captured.

Local development can run sample modules through the file-based module contract:

```json
"ControlPlane__LocalModules__Enabled": "true",
"ControlPlane__LocalModules__Root": "",
"ControlPlane__LocalModules__WorkRoot": ""
```

When enabled, local dispatch looks for a matching project under `modules/<module-id>/src/<PascalCaseModuleId>`. For `tenant-health-check`, the dispatcher writes the standard job input to `.work/local-modules/<job-id>/input/job.json`, runs the module with `dotnet run`, reads `.work/local-modules/<job-id>/output/result.json`, and stores the output on the job record.

If no local module project is found, dispatch falls back to the simulated worker path. Deployed Azure environments leave local module execution disabled and will use the simulated path until Container Apps Jobs are wired.

Deployed environments can use Service Bus by setting:

```json
"ControlPlane__QueueProvider": "ServiceBus",
"ControlPlane__ServiceBusConnectionString": "<service-bus-connection-string>",
"ControlPlane__JobQueueName": "jobs",
"ServiceBusConnection": "<service-bus-connection-string>",
"ServiceBusJobQueueName": "jobs"
```

The simple `ServiceBusConnection` and `ServiceBusJobQueueName` settings are used by the Functions trigger binding. The `ControlPlane__...` settings are used by the application queue provider.

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
GET  /api/jobs
GET  /api/jobs/{id}
GET  /api/local/job-queue
POST /api/local/dispatch-next
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

List recent jobs:

```powershell
Invoke-RestMethod -Uri 'http://localhost:7071/api/jobs' -Method Get
```

List audit events:

```powershell
Invoke-RestMethod -Uri 'http://localhost:7071/api/audit-events' -Method Get
```

View the local in-memory queue snapshot:

```powershell
Invoke-RestMethod -Uri 'http://localhost:7071/api/local/job-queue' -Method Get
```

Dispatch the next queued local job:

```powershell
Invoke-RestMethod -Uri 'http://localhost:7071/api/local/dispatch-next' -Method Post
```

After dispatch, load the job again to inspect the module output:

```powershell
Invoke-RestMethod -Uri 'http://localhost:7071/api/jobs/{jobId}' -Method Get
```

## Current MVP Limits

- Data is stored in memory and disappears when the Function App restarts.
- Operator identity is stubbed as `operator@local.dev`.
- Job dispatch is queued into an in-memory queue.
- Local dispatch runs a matching sample module when available; otherwise it simulates worker execution and marks the job as `Succeeded`.
- Service Bus dispatch uses the same `JobDispatcher` service when `ControlPlane__QueueProvider` is set to `ServiceBus`.
- Container Apps Jobs are not wired yet.
- Key Vault is not wired yet.
