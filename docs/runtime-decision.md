# Runtime Decision

## Decision

The control plane will use Azure Functions isolated .NET as the controller runtime.

The existing ASP.NET Core Web API scaffold is transitional and should be replaced before implementation begins.

## Reasoning

The control plane is primarily an event-driven coordinator. It receives requests, validates them, records state, sends messages, starts workers, receives callbacks, and exposes run history. It should not hold long-running work in memory or act as an always-on automation host.

Azure Functions fits this shape because it can wake up for:

- HTTP requests.
- Service Bus messages.
- Timer events.
- Worker callbacks.

Each function performs a focused control action, writes durable state, and exits.

## Durable State Reservoir

The Functions app coordinates around shared Azure services:

- Table Storage for tenants, modules, jobs, approvals, and job events.
- Blob Storage for large outputs, reports, and artifacts.
- Key Vault for secret references and credentials.
- Service Bus for queued job messages and dead-letter handling.

The controller does not rely on in-memory state.

## Event Flow

```text
Operator submits job
  -> HTTP Function validates request
  -> Job is written to Table Storage
  -> Message is sent to Service Bus
  -> Function exits

Service Bus message arrives
  -> Queue-triggered Function wakes up
  -> Function reads job and module metadata
  -> Function starts Container Apps Job
  -> Job status is updated to Running
  -> Function exits

Snap-in container completes
  -> Container calls callback HTTP Function
  -> Function validates callback
  -> Output and artifacts are stored
  -> Job status is updated
  -> Function exits
```

## Why Not An Always-On API First

An always-on ASP.NET Core API hosted in App Service or Container Apps would work, but it is not the best first fit for this project.

Azure Functions gives the MVP:

- Lower idle cost.
- Native event triggers.
- Good serverless alignment.
- Clear separation between control actions and automation execution.
- Natural integration with queues and timers.

The design should still keep clean contracts so the API layer could move later if the product needs richer always-on behavior.

