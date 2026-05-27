# Trigger Model

Triggers are a control plane concern, not a module concern.

Modules should remain dumb fire-on-command workers. They receive a standard job input, perform the requested work, and write structured output. They should not need to know whether the job was started manually, scheduled, webhook-driven, event-driven, or by another downstream system.

## Trigger Types

Initial trigger types:

- `Manual`
- `Scheduled`
- `Webhook`
- `Event`

## Control Plane Responsibilities

The control plane owns:

- trigger registration
- trigger enablement
- trigger authorization
- rate limits
- run history
- operator/audit attribution
- translation from trigger event to standard job request

Every trigger ultimately produces the same `SubmitJobRequest` shape already used by manual job submission.

```text
Trigger event
  -> Control plane validates trigger policy
  -> Control plane builds SubmitJobRequest
  -> Readiness check runs
  -> Job is queued
  -> Existing execution path starts the module
```

## Module Responsibilities

The module owns:

- reading the standard job input
- respecting target scope and parameters
- calling only APIs it has been granted through the client connection
- writing structured output

The module does not own:

- schedules
- webhook signatures
- trigger secrets
- retry policy
- rate limiting
- tenant routing

## Trigger Definition Shape

Future API shape:

```json
{
  "id": "trigger-monthly-account-report-contoso",
  "displayName": "Monthly account report - Contoso",
  "enabled": true,
  "type": "Scheduled",
  "clientConnectionId": "client-contoso-account-report",
  "moduleId": "msp-account-management-report",
  "moduleVersion": "0.1.3",
  "targetScope": {
    "type": "Tenant",
    "mode": "All",
    "targets": []
  },
  "parameters": {
    "includeInactiveUsers": true,
    "includeLicenseWaste": true,
    "reportFormat": "json"
  },
  "schedule": {
    "timezone": "Australia/Sydney",
    "cron": "0 9 1 * *"
  },
  "limits": {
    "minIntervalSeconds": 3600,
    "maxRunsPerDay": 3
  }
}
```

## Cost Abuse Controls

Trigger registration must support guardrails before automated execution is enabled:

- trigger-level enable/disable
- per-trigger minimum interval
- per-trigger daily run limit
- per-client concurrent run limit
- per-module cost tier awareness
- audit event for each trigger fire and each rejected fire

Manual runs should remain available even when automated trigger execution is disabled, provided the operator is authorized and readiness passes.

## Webhook Triggers

Webhook triggers should not expose the normal management API bearer token path.

Recommended model:

- each webhook trigger receives a dedicated endpoint or trigger ID
- inbound requests must include a signature or trigger secret
- raw inbound payload is stored only if explicitly configured
- trigger payload is transformed into the module's `parameters`, never into module identity or client connection fields

This preserves tenant isolation and keeps external callers from choosing arbitrary clients, modules, or scopes.

## Scheduled Triggers

Scheduled triggers should be implemented as a separate scheduler layer rather than embedded in module workers.

Candidate implementations:

- Azure Functions timer trigger
- Logic App recurrence
- Container Apps job scheduler wrapper
- external scheduler posting into a locked webhook trigger

The scheduler should call the same internal job submission service used by manual and webhook triggers.

## Data Consumers

Triggers can later chain into downstream consumers without changing module workers.

Examples:

- run module
- collect artifact
- fire notification webhook
- send raw JSON to BYOAI
- export to storage or Power BI

That keeps the control plane composable while preserving the module contract.
