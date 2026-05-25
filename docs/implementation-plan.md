# Implementation Plan

## Phase 1: Documentation and Repo Setup

- Confirm architecture direction.
- Confirm module contract.
- Confirm control plane service boundary.
- Create project folder and initial docs.
- Add `.gitignore`.
- Decide whether this becomes its own GitHub repository.

## Phase 2: Control Plane API MVP

- Replace the transitional ASP.NET Core scaffold with a .NET isolated Azure Functions project.
- Add health endpoint.
- Add client connection model.
- Add client tenant model.
- Add module registry model.
- Add supported target scope model.
- Add module manifest validation.
- Add module registration endpoint.
- Add module catalog endpoint.
- Add job submission endpoint.
- Add job status endpoint.
- Add Table Storage repositories.
- Add basic validation.

## Phase 3: Local Worker Loop

- Add a development worker that can process queued jobs locally.
- Add a simple health-check module.
- Prove the job input and output contract.
- Prove tenant-scope and user-scope execution payloads.
- Store run history and job events.

## Phase 4: Container Apps Job Execution

- Add Terraform for Container Apps Environment.
- Add Container Apps Job definition.
- Add worker image build and publish steps.
- Add Service Bus-triggered Function to dispatch queued jobs.
- Capture output artifacts.

## Phase 5: First Useful Module

Recommended first real module: Azure cost and governance report.

Initial report sections:

- Subscription summary.
- Resource group summary.
- Monthly cost trend where available.
- Budget status.
- Diagnostic settings coverage.
- Defender for Cloud posture.
- Policy assignment summary.
- Key Vault and storage account baseline checks.

This lines up well with AZ-305 because it touches governance, cost, monitoring, security, and operational design.

## Phase 6: Frontend

- Add client list.
- Add module catalog.
- Add target scope selector.
- Add job submission form generated from module parameter schema.
- Add job history.
- Add job result view.
- Add audit view.

## Phase 7: Terraform Deployment

- Add pre-discovery script.
- Add optional bootstrap script.
- Add deployment script.
- Resource group.
- Storage account.
- Key Vault.
- Function App.
- Static Web App.
- Service Bus.
- Application Insights.
- Log Analytics workspace.
- Container Apps Environment.
- Container registry or GitHub Container Registry integration.
- Configurable region, naming prefix, environment, and module source inputs.
- Add post-deployment script.
- Add optional teardown script.

## Phase 8: Hardening

- Add APIM optional deployment tier.
- Add Application Gateway/WAF optional deployment tier.
- Add role-based access.
- Add approval flow for high-risk modules.
- Add dead-letter queue handling.
- Add teardown script.
- Add deployment guide.
- Add sample tenant/client onboarding guide.
- Add threat model notes.
