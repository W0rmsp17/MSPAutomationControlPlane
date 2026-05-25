# Architecture

## High-Level Flow

```text
Admin UI
  -> HTTP-triggered Azure Function
  -> Tenant, module, parameter, and scope validation
  -> Job persistence and audit event
  -> Service Bus queue
  -> Service Bus-triggered Azure Function
  -> Container Apps Job worker
  -> Snap-in module
  -> Callback HTTP-triggered Azure Function
  -> Output artifact and status update
  -> Run history and audit view
```

## Main Components

### Frontend

The frontend gives operators a simple way to:

- View registered client tenants.
- Browse enabled automation modules.
- Register new snap-in modules through a management interface.
- Choose a supported target scope, such as tenant, users, groups, devices, subscriptions, or resource groups.
- Submit module jobs.
- Review job status and output.
- View audit history.

Azure Static Web Apps is the preferred first option because it is low-cost, simple to deploy, and fits a mostly static UI backed by APIs.

### Control Plane API

Azure Functions isolated .NET should host the control API and controller logic. It is responsible for:

- Authentication and authorization.
- Client tenant registration.
- Module registration and discovery.
- Module manifest validation.
- Job submission.
- Parameter validation.
- Target scope validation.
- Identity and secret reference brokering.
- Permission requirement checks.
- Writing job state.
- Queueing work.
- Receiving worker callbacks.
- Exposing run history.

The API should not run long automation tasks directly. The Functions app acts as an event-driven controller and mediation layer around durable Azure services.

The initial function set should include:

- `GET /api/tenants`
- `POST /api/tenants`
- `GET /api/modules`
- `POST /api/modules`
- `POST /api/jobs`
- `GET /api/jobs/{id}`
- `POST /api/jobs/{id}/approve`
- `POST /api/jobs/{id}/callback`
- Service Bus trigger for job dispatch.
- Timer trigger for stale job checks and scheduled maintenance.

Each function should do a small amount of work, persist state, and stop. The shared state reservoir is Table Storage, Blob Storage, Key Vault, and Service Bus.

### Control Services

The control plane should provide shared services that snap-ins can consume through the module and job contracts:

- Tenant selection.
- Target scope selection.
- Parameter form generation from module schemas.
- Secret reference resolution.
- Permission declaration and readiness checks.
- Approval policy.
- Job lifecycle management.
- Output storage and presentation.
- Audit history.

These services are the main reason for the platform. A snap-in should bring business logic, while the control plane handles the operational wrapper around it.

### Runtime Shape

The project will not use an always-on controller app for the MVP. The runtime shape is:

```text
Static Web App frontend
        |
Azure Functions control plane
        |
Table Storage + Blob Storage + Key Vault
        |
Service Bus queue
        |
Container Apps Jobs
```

This keeps the controller cheap at idle, event-driven, and aligned with the serverless design. If the platform later needs richer middleware or always-on APIs, the contract should still allow the API layer to move to Container Apps without changing snap-in modules.

Runtime health checks should be click-to-refresh for the MVP. Background polling can be added later if required.

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
- Supported target scopes.
- A declared permissions model.
- A declared output schema.

The module receives a standard job input and returns a standard output. This keeps the control plane reusable.

Modules should be registered through the control plane management API/UI. The MVP should support manual manifest registration first, with registry or repository discovery later.

## Deployment Package

This project should be deployable as a reusable package. Implementors should be able to configure:

- Azure region.
- Environment name.
- Resource naming prefix.
- Target subscription.
- Admin/operator group.
- Allowed module registries.
- Storage and queue options.
- Whether sample modules are deployed.

The deployment should not contain tenant-specific hard-coding. Demo modules may exist, but real automation modules should be optional snap-ins.

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
