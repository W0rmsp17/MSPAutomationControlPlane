# Architecture

## High-Level Flow

```text
Admin UI
  -> Control Plane API
  -> Job validation and persistence
  -> Service Bus queue
  -> Container Apps Job worker
  -> Snap-in module
  -> Output artifact and status update
  -> Run history and audit view
```

## Main Components

### Frontend

The frontend gives operators a simple way to:

- View registered client tenants.
- Browse enabled automation modules.
- Submit module jobs.
- Review job status and output.
- View audit history.

Azure Static Web Apps is the preferred first option because it is low-cost, simple to deploy, and fits a mostly static UI backed by APIs.

### Control Plane API

Azure Functions should host the API. It is responsible for:

- Authentication and authorization.
- Client tenant registration.
- Module registration and discovery.
- Job submission.
- Parameter validation.
- Writing job state.
- Queueing work.
- Receiving worker callbacks.
- Exposing run history.

The API should not run long automation tasks directly.

### Queue

Azure Service Bus is the recommended queue for the control plane because it gives better job semantics than a basic storage queue:

- Dead-letter queue.
- Message lock handling.
- Scheduled messages.
- Retry control.
- Better operational visibility.

Storage Queue can be considered for an ultra-low-cost variant, but Service Bus is the stronger default for a reusable automation platform.

### Worker Runtime

Azure Container Apps Jobs should execute snap-ins. This keeps module dependencies isolated and makes modules portable.

Each module can package its own runtime, for example:

- PowerShell with Microsoft Graph modules.
- .NET worker.
- Python automation.
- Azure CLI based checks.

The control plane starts or triggers workers with a standard job envelope. The worker retrieves required secrets through approved references and writes structured output.

### State Store

Azure Table Storage is the recommended MVP state store because it is low-cost and good enough for simple entities:

- Clients.
- Modules.
- Jobs.
- Job events.
- Artifacts.

The code should hide storage behind repository interfaces so a future move to Cosmos DB or Azure SQL does not reshape the whole app.

### Secrets

Azure Key Vault stores secrets and certificates. Job payloads should carry secret references only, never raw secrets unless there is no practical alternative.

Examples:

- `kv://client-a/graph-client-secret`
- `kv://client-a/automation-certificate`
- `kv://shared/storage-report-key`

### Observability

Application Insights and Log Analytics should capture:

- API requests.
- Job submission events.
- Worker start and completion.
- Module failures.
- Queue dead-letter events.
- Security-sensitive operations.

Run history should be useful without opening Azure Portal, but Azure-native logs should remain available for deeper troubleshooting.

## Snap-In Boundary

The control plane should not need custom code for every module. A module is snapped in by publishing:

- A module manifest.
- A container image.
- A parameter schema.
- A declared permissions model.
- A declared output schema.

The module receives a standard job input and returns a standard output. This keeps the control plane reusable.

## Security Model

Recommended baseline:

- Use Microsoft Entra authentication for the UI/API.
- Use application roles or groups for operator access.
- Use managed identity for the control plane's Azure resource access.
- Grant Key Vault access through RBAC.
- Keep per-client credentials separate.
- Record all job submissions, approvals, and executions.
- Require approval for high-risk modules.
- Disable modules per client unless explicitly enabled.

## MVP Scope

The first implementation should prove the platform loop:

1. Register a client.
2. Register a module.
3. Submit a job.
4. Queue the job.
5. Run a container worker.
6. Return structured output.
7. Display status and history.

Advanced scheduling, multi-region execution, marketplace-style module publishing, and complex approval chains should come later.

