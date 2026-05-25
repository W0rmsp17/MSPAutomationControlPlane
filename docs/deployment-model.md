# Deployment Model

This project should be deployable through Terraform with supporting scripts for discovery, deployment, and post-deployment configuration.

The deployment should be reusable by other implementors and should not contain tenant-specific hard-coding.

The MVP deployment creates the central MSP-hosted control plane only. Client tenants are added after deployment through client connection registration.

## Deployment Goals

- Portable across regions.
- Configurable naming and environment values.
- Low-touch setup for a privileged implementor.
- Clear manual fallback points.
- Repeatable Terraform deployment.
- Post-deployment configuration for generated URLs and app settings.
- Teardown path for lab or portfolio users.

## Script Flow

Recommended script sequence:

```text
pre-discover.ps1
  -> Discover tenant ID, subscription ID, locations, signed-in account, and defaults.

bootstrap.ps1
  -> Optional privileged setup such as app registrations, identities, and Key Vault seed values.

deploy.ps1
  -> Terraform init/validate/plan/apply.

deploy-function.ps1
  -> Publish and zip-deploy the .NET isolated Azure Functions control API.

deploy-frontend.ps1
  -> Inject the API base URL and deploy the static management UI to Azure Static Web Apps.

post-deploy.ps1
  -> Read Terraform outputs and print endpoint/runtime values.

teardown.ps1
  -> Optional cleanup for lab environments.
```

## Terraform Responsibilities

Terraform should deploy:

- Resource group.
- Storage account.
- Table Storage resources where practical.
- Blob containers for artifacts.
- Key Vault.
- Function App for the control plane.
- Service Bus namespace and queues.
- Application Insights.
- Log Analytics workspace.
- Static Web App or frontend hosting.
- Container Apps Environment.
- Managed identities and RBAC assignments.

Subscriptions using Container Apps must have the `Microsoft.App` resource provider registered before the Container Apps Environment can be created.

## Configurable Inputs

Initial deployment inputs:

- Environment name.
- Azure region.
- Resource naming prefix.
- Subscription ID.
- Tenant ID.
- Operator/admin group object ID.
- Allowed container registry list.
- Storage SKU.
- Service Bus SKU.
- Whether sample modules are enabled.
- Runtime health refresh mode.

## Runtime Health Mode

The MVP should use click-to-refresh runtime health checks.

No background polling should be enabled by default. This keeps cost low and avoids noisy telemetry. Timer-based health snapshots can be added later if needed.

## Bootstrap Notes

Bootstrap should support both automated and manual paths.

Automated bootstrap can create or configure required identities and app registrations where permissions allow. Manual bootstrap should document what values the implementor must provide if their organisation prefers to pre-create privileged objects.

## Post-Deployment Notes

Post-deployment should read Terraform outputs and update runtime settings such as:

- API base URL.
- Frontend URL.
- Callback base URL.
- Storage references.
- Service Bus queue names.
- Allowed module registry configuration.

Post-deployment should also print a concise deployment summary for the implementor.

The deployment script supports non-interactive lab deployment through `-Apply -AutoApprove`. Production deployments can omit `-AutoApprove` to keep the Terraform approval prompt.
