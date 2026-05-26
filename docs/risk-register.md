# Architecture Risk Register

This document tracks known blind spots and design decisions for the MSP Automation Control Plane. It is intentionally public-safe: it describes architecture risks without tenant-specific values, secrets, or lab details.

## Current Position

The MVP now has a central MSP-hosted control plane, protected management UI, Azure Functions API, Service Bus dispatch, Table Storage state, Container Apps Job worker slot, module manifest validation, and blob-based result collection.

The design is suitable for a lab and portfolio demonstration, but production MSP usage needs hardening in the areas below.

## High Priority

### Container Identity Isolation

Risk:
A module container that processes Client A data could accidentally or maliciously access credentials or artifacts for Client B.

Current mitigation:
The module job receives a specific client connection ID and a scoped job payload. Secrets are represented as references, not raw values. Output is written to a per-job blob path.

Remaining work:
Add per-client secret scopes in Key Vault, enforce per-job secret allow-lists, and avoid giving the generic worker identity broad access to all client secrets. For stronger isolation, consider per-client user-assigned managed identities or per-client worker job definitions.

### Graph And Downstream API Throttling

Risk:
Concurrent jobs for the same client tenant could overload Microsoft Graph or another downstream API, especially when many modules use the same app registration.

Current mitigation:
Module manifests include concurrency metadata, but dispatch does not yet enforce per-client throttling.

Remaining work:
Add per-client concurrency controls. Service Bus sessions keyed by client connection ID are a good fit when jobs for the same client must run sequentially. Also add retry/backoff guidance for module authors.

### Container Image Provenance

Risk:
The control plane could run an untrusted or mutable container image.

Current mitigation:
Manifest validation only allows configured registry hostnames. CI/CD builds and publishes module images. Terraform supports private registries.

Remaining work:
Prefer digest-pinned images, record image digest at module registration time, and optionally require trusted publisher metadata or image signing before a module can be enabled.

### Result And Log Exfiltration

Risk:
Module logs or outputs could accidentally include tenant data, secrets, access tokens, or PII.

Current mitigation:
The module contract expects structured output and artifacts. Secrets should be referenced rather than embedded.

Remaining work:
Document module logging rules, redact known secret patterns before storing output, keep raw worker logs in Log Analytics with restricted access, and avoid surfacing container stdout directly to operators without filtering.

## Medium Priority

### Service Bus Message Settlement

Risk:
The dispatcher could start a Container Apps Job and then fail before recording state, or retry and start duplicate executions.

Current mitigation:
The dispatcher updates the job to `Running` after the execution provider starts work. Failed dispatch throws so Service Bus retry/dead-letter behavior applies.

Remaining work:
Add idempotency fields such as execution name and dispatch attempt ID to the job record. Consider a separate `ExecutionStarted` record before acknowledging the queue message.

### ACA Provisioning Failures And Cold Starts

Risk:
Container Apps Jobs may take time to pull images or may fail to provision due to image pull errors, quota, registry auth, or regional capacity.

Current mitigation:
Container Apps Jobs have infrastructure-level timeout and retry settings. The control plane starts jobs asynchronously instead of waiting inside the HTTP request path.

Remaining work:
Add execution polling and failure classification. Surface image-pull failures, timeout, quota, and worker exit-code failures separately in the UI.

### Worker Exit Code Contract

Risk:
Different modules may use inconsistent exit codes, making automated triage unreliable.

Current mitigation:
The sample module uses simple exit codes for input and output failures.

Remaining work:
Standardize module exit codes in `docs/module-contract.md` and require modules to emit structured failure output where possible.

### Table Storage Size Limits

Risk:
Large job outputs may exceed practical Table Storage entity limits or make job list queries heavy.

Current mitigation:
The architecture now stores Container Apps output in Blob Storage first, then copies the structured result into the job record.

Remaining work:
Keep only summary, status, metrics, and small findings in Table Storage. Store large outputs and reports as blob artifacts with references in the job output.

### Manifest Breaking Changes

Risk:
A new module version could remove or change parameters required by existing per-client configuration.

Current mitigation:
Modules are versioned by manifest ID and version. Registration validates shape but does not yet enforce compatibility.

Remaining work:
Treat module versions as immutable. Add compatibility checks before enabling a new version for a client. Keep old versions available until dependent client configurations are migrated.

## Lower Priority Or Future Hardening

### Data Residency

Risk:
An MSP may need data for different client tenants to stay in specific regions.

Current mitigation:
Terraform exposes region configuration for the central deployment.

Remaining work:
Document deployment-per-region patterns. For strict residency, use separate control plane deployments or regional worker/storage partitions.

### App Registration And Certificate Lifecycle

Risk:
Client app credentials or certificates may expire without warning.

Current mitigation:
Client connection records can store readiness metadata and notes.

Remaining work:
Add certificate expiry metadata, readiness checks, and UI alerts for expiring credentials.

### Schema Migrations

Risk:
Table Storage is schemaless, so future code changes may expect fields missing from older deployments.

Current mitigation:
The current model is early and repository-backed, so storage implementation can evolve.

Remaining work:
Add schema version fields to stored entities and migration scripts for breaking changes.

### Static Web App Deployment Friction

Risk:
Static Web Apps deployment can require a deployment token or GitHub Actions integration, which adds setup friction.

Current mitigation:
The repo includes `deploy-frontend.ps1`, which packages and deploys the static frontend using the SWA deployment token from Azure CLI.

Remaining work:
Document both supported paths: script-based deployment for labs and GitHub Actions deployment for long-lived environments.
