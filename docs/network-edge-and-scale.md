# Network Edge And Scale

The MVP should keep the runtime small, but the architecture should allow stronger edge and scale options as the platform grows.

## MVP Edge

Initial public entry point:

```text
Static Web App frontend
  -> Azure Functions HTTP API
```

This is sufficient for early local/MVP work because authentication, authorization, and API contracts can be developed without adding a gateway layer.

## Optional API Management Layer

Azure API Management can sit in front of the Functions API.

```text
Static Web App frontend
  -> API Management
  -> Azure Functions control API
```

APIM is useful for:

- Central API gateway policy.
- Rate limiting.
- Request/response validation.
- API versioning.
- Product/subscription style API exposure.
- Better developer-facing API documentation.
- Auth policy enforcement.
- Future third-party module publisher integration.

Tradeoffs:

- Additional monthly cost.
- More infrastructure to deploy and operate.
- More policy/configuration surface.

Recommendation:

- Do not require APIM for MVP.
- Add APIM as an optional deployment tier.
- Use APIM when the platform exposes APIs beyond the first-party management UI or needs stronger API governance.

## Optional Application Gateway Layer

Application Gateway can be used when the platform needs web application firewall, private networking, or more controlled ingress.

```text
Operator
  -> Application Gateway / WAF
  -> Frontend/API origin
```

App Gateway is useful for:

- WAF policies.
- Private endpoint patterns.
- Central ingress control.
- Enterprise network integration.
- Custom routing.

Tradeoffs:

- Higher operational and cost overhead than the MVP requires.
- Less directly useful for serverless APIs unless there is a broader private networking requirement.

Recommendation:

- Do not use App Gateway in the MVP.
- Keep it as an enterprise deployment option.
- Prefer APIM first for API governance.

## Scale-Out Model

The architecture should scale horizontally by separating concerns:

```text
HTTP Functions
  -> Validate and persist requests

Service Bus
  -> Buffers work

Dispatch Functions
  -> Start workers

Container Apps Jobs
  -> Execute snap-in modules

Storage
  -> Stores durable state and artifacts
```

Scale is driven by events and queue depth rather than a single always-on controller process.

## Function App Scale

Azure Functions can scale HTTP and trigger execution based on demand. The control plane should keep function handlers short:

- Validate request.
- Read/write state.
- Send message.
- Return response.

Long-running module work belongs in Container Apps Jobs.

## Queue-Based Back Pressure

Service Bus is the buffer between operators and execution.

Benefits:

- Smooths bursts of job submissions.
- Allows retry and dead-letter handling.
- Lets dispatch scale independently.
- Protects downstream APIs and tenant rate limits.

The control plane should support per-module and per-client concurrency limits.

## Worker Scale

Container Apps Jobs should scale by job count and module type.

Controls to model:

- Module concurrency.
- Client concurrency.
- Global worker concurrency.
- Timeout seconds.
- Retry count.
- Maximum target count per job.

## State Store Scale

Table Storage is sufficient for MVP entities, but entity partitioning should be planned.

Suggested partition keys:

- Clients: `CLIENT`
- Modules: `MODULE`
- Jobs: `CLIENT#{clientId}` or `MODULE#{moduleId}` depending access pattern.
- Job events: `JOB#{jobId}`

If queries become richer, move behind the repository interfaces to Cosmos DB or Azure SQL.

## Observability Scale

The control plane should store operational state in its own job/event tables and use Azure telemetry for deeper troubleshooting.

Click-to-refresh runtime health remains the MVP default. Scheduled health snapshots can be added later if operators need trend data.

## Deployment Tiers

### Tier 1: MVP

- Static Web App.
- Azure Functions.
- Service Bus.
- Storage.
- Key Vault.
- Container Apps Jobs.
- App Insights and Log Analytics.

### Tier 2: API Governance

Tier 1 plus:

- API Management.
- API policies.
- Versioned API products.

### Tier 3: Enterprise Edge

Tier 2 plus:

- Application Gateway/WAF where required.
- Private endpoints.
- Network restrictions.
- More formal publisher/module onboarding controls.

