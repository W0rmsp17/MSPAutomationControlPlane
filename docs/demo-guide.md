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
2. Client connection preview and readiness metadata.
3. Module manifest preview.
4. Guided job compose plus readiness check.
5. Job result collection and structured output.

Keep each GIF around 10-15 seconds.

## Suggested Screen Recording

Target length: 3-5 minutes.

Suggested structure:

1. State the MSP problem.
2. Show the architecture diagram.
3. Open the protected management UI.
4. Show client connection and module readiness.
5. Compose and submit a job.
6. Collect job output.
7. Explain why the design is low-cost and secure.

## Demo Script

Short script:

```text
This is an MSP automation control plane built on Azure serverless services.

The goal is not one script. The goal is a reusable platform where automations can be added as modules while the control plane handles tenant selection, permissions, job history, audit, and execution.

The operator signs in with Microsoft Entra. A client connection defines which tenant can be targeted, what modules are enabled, what scopes are allowed, and whether required permissions are ready.

Before a job runs, the platform checks readiness. If permissions, scopes, or module enablement are not right, the job is blocked before execution.

When a job is submitted, the API queues it through Service Bus. A Function dispatcher starts a Container Apps Job, the module writes structured output to Blob Storage, and the control plane collects the result back into job history.

This pattern keeps the platform low-cost at idle while still giving MSPs repeatable, auditable automation across client environments.
```
