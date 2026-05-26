# Notification Integration Model

The central MSP control plane should expose notifications as a webhook integration capability rather than hardcoding email, Teams, or ticketing logic.

This lets each MSP connect the platform to their preferred workflow:

- Teams workflow.
- Logic App.
- Power Automate.
- ITSM or PSA tool.
- Email relay.
- SIEM.
- Custom API.

## MVP Direction

Notification subscriptions should be registered through the management API/UI.

Initial API shape:

```text
POST   /api/notification-subscriptions
GET    /api/notification-subscriptions
DELETE /api/notification-subscriptions/{id}
```

The local MVP implements this registry surface only. Webhook delivery comes later after the platform event pipeline is stable.

Example subscription:

```json
{
  "name": "MSP Ops Teams Workflow",
  "url": "https://example.invalid/workflows/example",
  "eventTypes": [
    "JobFailed",
    "ApprovalRequired",
    "RuntimeHealthFailed"
  ],
  "enabled": true
}
```

## Notification Events

Initial event types:

- `JobSucceeded`
- `JobFailed`
- `ApprovalRequired`
- `ModuleRegistered`
- `ClientConnectionRegistered`
- `ClientConnectionUnhealthy`
- `PermissionReadinessFailed`
- `RuntimeHealthFailed`

## Event Payload

Notification events should use a standard payload.

```json
{
  "eventId": "evt-20260525-000001",
  "eventType": "JobFailed",
  "occurredAt": "2026-05-25T10:10:00Z",
  "severity": "Error",
  "summary": "Tenant health check failed.",
  "client": {
    "id": "client-contoso",
    "name": "Contoso"
  },
  "module": {
    "id": "tenant-health-check",
    "version": "0.1.0"
  },
  "jobId": "job-20260525100348-example",
  "links": {
    "job": "https://control.example.com/jobs/job-20260525100348-example"
  }
}
```

## Delivery Controls

Webhook delivery should be treated as an integration boundary.

MVP controls:

- Require HTTPS URLs.
- Store webhook signing secret as a Key Vault reference.
- Include a signature header on delivery.
- Log delivery attempts.
- Retry with backoff.
- Dead-letter failed notification deliveries.
- Allow subscriptions to be disabled.

## Implementation Timing

Notifications should consume platform events. They should not be built before job events and audit history are stable.

Recommended sequence:

1. Job and module event model.
2. Notification subscription model.
3. Manual webhook delivery from job events.
4. Queue-based notification delivery.
5. Retry and dead-letter handling.
