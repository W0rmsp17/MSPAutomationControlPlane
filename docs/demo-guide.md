# Demo Guide

This guide is for portfolio screenshots, GIFs, or a short screen recording. Keep the demo tenant-agnostic and avoid showing real tenant IDs, user principal names, subscription IDs, secrets, or client data.

## Core Message

MSPs often have useful automations, but they are hard to reuse safely because each script usually needs its own setup, permissions, inputs, logging, and audit trail.

This project provides the reusable control plane around those automations:

- client tenant registration
- module registration
- readiness checks
- guided job submission
- queue-based execution
- isolated container workers
- structured results
- audit history

## Suggested GIFs

Use short focused GIFs in the README or project page:

1. Operator sign-in and dashboard load.
2. Demo view import module, register client, and submit job.
3. Container Apps execution appears in job history.
4. Job result collection and rendered account report output.
5. Module catalog showing the imported account-management report release.

Keep each GIF around 10-15 seconds.

## Suggested Screen Recording

Target length: 3-5 minutes.

Suggested structure:

1. State the MSP problem.
2. Show the architecture diagram.
3. Open the protected management UI.
4. Use the Demo view to import the account-management report module release.
5. Register the demo client connection.
6. Submit the account report job.
7. Collect job output and show the rendered report.
8. Explain why the design is low-cost and secure.

## Demo View Flow

The management UI includes a Demo view for the account-management report module.

Recommended run:

1. `Import module`
2. `Register client`
3. `Submit job`
4. Wait for the Container Apps Job execution to complete.
5. `Collect result`

The Demo view uses public-safe placeholder tenant and app IDs. It validates the control-plane execution path and renders the module output, but it does not prove live Microsoft Graph collection until the client connection is backed by real target-tenant credentials.

Expected result:

- Job moves from `Queued` to `Running`.
- Container Apps execution reaches `Succeeded`.
- Result collection moves the control-plane job to `Succeeded`.
- Rendered Markdown report appears in the Demo view.

## Demo Script

Short script:

```text
This is an MSP automation control plane built on Azure serverless services.

The goal is not one script. The goal is a reusable platform where automations can be added as modules while the control plane handles tenant selection, permissions, job history, audit, and execution.

The operator signs in with Microsoft Entra. A client connection defines which tenant can be targeted, what modules are enabled, what scopes are allowed, and whether required permissions are ready.

Before a job runs, the platform checks readiness. If permissions, scopes, or module enablement are not right, the job is blocked before execution.

When a job is submitted, the API queues it through Service Bus. A Function dispatcher starts a Container Apps Job, the module writes structured output to Blob Storage, and the control plane collects the result back into job history.

The account-management report demo shows this with a real module image. The module receives a standard input payload, writes output through the standard blob output URI, and returns license and user signals in a consistent report format.

This pattern keeps the platform low-cost at idle while still giving MSPs repeatable, auditable automation across client environments.
```
