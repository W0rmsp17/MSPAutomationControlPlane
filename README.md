# MSP Automation Control Plane

A lightweight Azure control plane for running repeatable MSP automation modules across client environments.

The goal is to provide a portable, low-cost, API-driven platform where automation "snap-ins" can be added without rebuilding the core application. Each snap-in should declare what it needs, accept a standard job payload, run in an isolated worker, and return structured results that the control plane can audit and display.

## Problem Statement

MSPs often build useful automations, but they become hard to reuse because each script has its own setup, credentials, inputs, logging, and execution pattern. This project aims to standardise the surrounding platform:

- Register client tenants and execution contexts.
- Expose approved automation modules through a simple UI/API.
- Queue and run jobs reliably.
- Store run history, outputs, and audit events.
- Keep secrets in Key Vault rather than inside scripts or app settings.
- Allow modules to be replaced or extended without changing the control plane.

## Initial Direction

The control plane should be serverless-first, but not force every automation to run inside Azure Functions. Functions are a good fit for APIs, orchestration, validation, and short tasks. Container-based workers are a better fit for snap-ins because modules may need different SDKs, PowerShell modules, CLIs, or runtime versions.

Recommended MVP architecture:

- Frontend: Azure Static Web Apps or App Service.
- API: Azure Functions using .NET.
- Queue: Azure Service Bus for job dispatch.
- Worker runtime: Azure Container Apps Jobs for snap-in modules.
- State: Azure Table Storage for the first version, with a clean repository layer so it can move to Cosmos DB or Azure SQL later.
- Secrets: Azure Key Vault.
- Identity: Managed identities for Azure resources, with per-client Microsoft Entra app registrations or federated identity where needed.
- Observability: Application Insights and Log Analytics.
- Infrastructure: Terraform.

## Core Concepts

Client tenant:
Represents a managed customer or internal environment. Stores non-secret metadata such as tenant ID, display name, default region, and enabled modules.

Automation module:
A reusable snap-in that declares its metadata, required permissions, parameters, image, timeout, and output schema.

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
- Secrets are referenced, not embedded in job payloads.
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

This repository is currently documentation-first. Implementation should start after the architecture and module contract are agreed.

