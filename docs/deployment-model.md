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

ensure-swa-auth-app.ps1
  -> Create or reuse an MSP-tenant Entra app registration for browser MSAL sign-in, expose an API scope, and configure Function API token validation.

deploy-function.ps1
  -> Publish and zip-deploy the .NET isolated Azure Functions control API.

deploy-frontend.ps1
  -> Inject the API base URL and MSAL client settings, then deploy the static management UI to Azure Static Web Apps.

post-deploy.ps1
  -> Read Terraform outputs and print endpoint/runtime values.

teardown.ps1
  -> Preview or run cleanup for lab environments, including Terraform resources and the script-managed Entra auth app registration.
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

The Static Web App serves static frontend files. Access control is enforced at the Function API boundary, not by Static Web Apps platform authentication. The management UI uses MSAL with an MSP-tenant Microsoft Entra app registration to request an `access_as_user` token, then sends it as a bearer token to the Function App.

The Function App validates issuer, audience, token lifetime, signing keys, required scope, and operator authorization. Operator authorization can be controlled through allowed user object IDs, group object IDs, or app roles. The bootstrap script defaults API access to the signed-in implementor so a fresh deployment is not accidentally open to every authenticated MSP tenant user.

The first operator sign-in after the API scope is created may require Microsoft Entra consent for the management UI to call the control-plane API. For MSP production use, prefer an operator group and run:

```powershell
.\scripts\ensure-swa-auth-app.ps1 -AllowedGroupIds "<operator-group-object-id>"
```

For lab deployments or small MSP environments, the script can also create or reuse a named group and add the signed-in implementor:

```powershell
.\scripts\ensure-swa-auth-app.ps1 `
  -CreateOperatorGroup `
  -OperatorGroupDisplayName "MSP Control Plane Operators" `
  -AddSignedInUserToOperatorGroup
```

For lab deployments or a single implementor, the default signed-in user object ID is enough.

## Configurable Inputs

Initial deployment inputs:

- Environment name.
- Azure region.
- Resource naming prefix.
- Subscription ID.
- Tenant ID.
- Operator/admin group object ID.
- Optional allowed operator user object IDs.
- Optional allowed operator group object IDs.
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

## Teardown Notes

The teardown script is intentionally dry-run by default. It runs a Terraform destroy plan and reports the script-managed Static Web App/API auth app registration that would be deleted.

```powershell
.\scripts\teardown.ps1 -Environment "<environment>"
```

To remove the lab deployment:

```powershell
.\scripts\teardown.ps1 -Environment "<environment>" -Destroy -AutoApprove
```

The Entra app registration is not Terraform-managed because it is configured after the Static Web App hostname exists. `teardown.ps1` deletes it by exact client ID when `-AuthAppClientId` is supplied, otherwise by the default display name. If the app registration is shared or manually owned, pass `-KeepAuthApp`.

Terraform destroy can occasionally lag behind Azure resource deletion, especially for Static Web Apps, Function Apps, and monitoring resources. If a destroy times out after most resources are gone, verify the resource group contents before retrying or manually removing the remaining script-created resource. Do not assume a timeout means all resources still exist.
