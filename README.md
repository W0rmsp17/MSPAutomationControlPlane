# MSP Automation Control Plane

A lightweight Azure control plane for running repeatable MSP automation modules across client environments.

The goal is to provide a portable, low-cost, API-driven platform where automation "snap-ins" can be added without rebuilding the core application. Each snap-in should declare what it needs, accept a standard job payload, run in an isolated worker, and return structured results that the control plane can audit and display.

This repository focuses on the control plane itself: deployment, API, orchestration, tenant/client registry, target scoping, identity/secret brokering, queueing, audit, and module registration. Business-specific automation modules can be built as separate projects that plug into the platform contract.

## Problem Statement

MSPs often build useful automations, but they become hard to reuse because each script has its own setup, credentials, inputs, logging, and execution pattern. This project aims to standardise the surrounding platform:

- Register client tenants and execution contexts.
- Expose approved automation modules through a simple UI/API.
- Queue and run jobs reliably.
- Store run history, outputs, and audit events.
- Keep secrets in Key Vault rather than inside scripts or app settings.
- Allow modules to be replaced or extended without changing the control plane.
- Let module authors focus on business logic instead of rebuilding tenant pickers, approval flows, job tracking, secret handling, and audit logging.

## Product Boundary

This project should produce a reusable deployment package that others can configure for their own region, tenant, subscription, naming rules, and module sources.

The control plane provides:

- Deployable Azure infrastructure.
- API surface for tenants, modules, jobs, scopes, approvals, and results.
- Module registry and management interface for adding snap-ins.
- Standard app container registration contract.
- Standard job input and output contract.
- Shared operator interface.
- Shared security, audit, and observability.

The control plane does not provide every automation. Instead, it provides the platform that makes automations easy to snap in.

The primary deployment model is a central MSP-hosted control plane. Managed client tenants are connected through explicit client connection records that define the tenant ID, execution identity, enabled modules, allowed scopes, and permission readiness.

## Initial Direction

The control plane should be serverless-first, but not force every automation to run inside Azure Functions. Functions are a good fit for APIs, orchestration, validation, and short tasks. Container-based workers are a better fit for snap-ins because modules may need different SDKs, PowerShell modules, CLIs, or runtime versions.

Recommended MVP architecture:

- Frontend: Azure Static Web Apps or App Service.
- API: Azure Functions using .NET isolated worker.
- Queue: Azure Service Bus for job dispatch.
- Worker runtime: Azure Container Apps Jobs for snap-in modules.
- State: Azure Table Storage for the first version, with a clean repository layer so it can move to Cosmos DB or Azure SQL later.
- Secrets: Azure Key Vault.
- Identity: Managed identities for Azure resources, with per-client Microsoft Entra app registrations or federated identity where needed.
- Observability: Application Insights and Log Analytics.
- Infrastructure: Terraform.

Optional enterprise edge components such as API Management and Application Gateway should be deployment tiers, not MVP requirements.

## Runtime Decision

The controller layer will use an event-driven Azure Functions model rather than an always-on ASP.NET Core controller app.

In this model, the control plane is a set of focused functions:

- HTTP-triggered functions for operator/API requests.
- Service Bus-triggered functions for queued job dispatch.
- Timer-triggered functions for cleanup, stale job checks, and scheduled maintenance.
- Callback functions for snap-in job completion.

Functions wake up for a specific event, read and update shared platform state, call the required Azure service, then finish. Durable state lives in Table Storage, Blob Storage, Key Vault, and Service Bus rather than in a long-running process.

The current ASP.NET Core scaffold is transitional and should be replaced with a .NET isolated Azure Functions project before implementation begins.

## Deployment Direction

Infrastructure will be deployed with Terraform and supported by PowerShell deployment scripts.

The intended flow is:

- Pre-discovery script for tenant, subscription, account, and region defaults.
- Optional bootstrap script for privileged setup.
- Deployment script for Terraform.
- Function App deployment script for the control API runtime.
- Post-deployment script for generated URLs and runtime settings.
- Optional teardown script for lab environments.

The first Terraform deployment target is the central MSP environment. Client tenants are registered later as `ClientConnection` records rather than receiving their own management plane deployment.

### Deployment Commands

Create an environment-specific `terraform.tfvars` file from the relevant example under `infra/environments/<environment>`, then run:

```powershell
.\scripts\deploy.ps1 -Environment cholbing-dev -Apply -AutoApprove
.\scripts\deploy-function.ps1
.\scripts\post-deploy.ps1
```

If Container Apps is used for worker execution, the Azure subscription must have the `Microsoft.App` resource provider registered:

```powershell
az provider register --namespace Microsoft.App
```

## Core Concepts

Client tenant:
Represents a managed customer or internal environment. Stores non-secret metadata such as tenant ID, display name, default region, and enabled modules.

Automation module:
A reusable snap-in that declares its metadata, required permissions, parameters, image, timeout, and output schema.

Target scope:
The object set a job runs against. A module can support tenant-wide execution, selected users, multiple users, groups, devices, subscriptions, resource groups, or custom object lists.

Job:
A requested execution of a module against a client tenant. Jobs are submitted through the API, queued, executed by a worker, and recorded in run history.

Run output:
Structured result returned by the module. Outputs should include status, summary, findings, metrics, and artifact references.

Approval:
Optional workflow step for high-risk modules. Some modules may run immediately; others may require approval before execution.

## Design Principles

- The control plane owns scheduling, state, security, audit, and visibility.
- Snap-ins own their specific automation logic.
- Snap-ins communicate through a versioned contract, not bespoke control plane APIs.
- Snap-ins bring business logic; the control plane provides common platform services.
- Secrets are referenced, not embedded in job payloads.
- Target scope is first-class in every job request.
- Long-running or dependency-heavy work runs in containers, not HTTP request handlers.
- Tenant boundaries should be explicit and visible in every job request.
- The first version should be simple enough to deploy, demo, and reason about.

## Candidate First Snap-Ins

Good first modules:

- Health check module that validates the job contract and returns basic environment details.
- Azure cost and governance report for a small business or MSP client.
- Microsoft 365 license usage report.
- Defender and policy baseline report.

The health check module should come first because it proves the control plane can register a module, submit a job, run a container, collect output, and display history before any real business logic is added.

## Repository Status

This repository now has a deployable MVP foundation:

- .NET 8 isolated Azure Functions control API.
- HTTP functions for health, modules, client connections, notification subscriptions, jobs, and audit.
- Service Bus-triggered simulated dispatch flow.
- Table Storage persistence provider.
- Terraform deployment for the central MSP control plane.
- PowerShell scripts for pre-discovery, Terraform deployment, Function App zip deployment, and post-deployment outputs.

The first live deployment has validated health checks and an end-to-end job flow through Azure Functions, Table Storage, and Service Bus.
