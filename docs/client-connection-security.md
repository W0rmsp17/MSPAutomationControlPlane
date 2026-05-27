# Client Connection Security Model

The primary deployment model is a central MSP-hosted control plane.

```text
MSP Azure tenant/subscription
  -> Management UI
  -> Azure Functions control API
  -> Module registry
  -> Job history and audit
  -> Service Bus
  -> Container Apps Jobs
  -> Key Vault

Client tenant
  -> App registration or workload identity boundary
  -> Granted permissions required by enabled modules
```

The control plane is centralised, but every job runs against an explicit client connection and target scope.

## Client Connection

A client connection represents one managed customer tenant or execution boundary.

Initial properties:

- Client display name.
- Client tenant ID.
- Execution mode.
- App registration client ID.
- Certificate Key Vault reference.
- Service principal object ID in the target tenant.
- Enabled modules.
- Allowed target scopes.
- Required permission readiness state: `Draft`, `PendingConsent`, `Ready`, or `Blocked`.
- Configured permissions with provider, permission name, type, and admin consent state.
- Approval policy.
- Created/updated audit metadata.

The control plane should never store raw client secrets in job payloads or module manifests.

## Recommended MVP Credential Model

For the MVP, use certificate-based app registration authentication per client tenant.

```text
Control Plane Function / Worker
  -> Uses managed identity
  -> Reads certificate from MSP Key Vault
  -> Requests token for client tenant app registration
  -> Calls Graph or Azure API in the client tenant
```

This gives a clear API boundary while keeping credentials controlled:

- No client secret strings.
- Certificate private key stored in Key Vault.
- Key Vault access granted to managed identity.
- Job payloads contain references only.
- Client permissions are isolated per client connection.
- Certificate rotation can happen per client.

## Operator Identity vs Execution Identity

Audit must distinguish who requested the work from what identity performed the work.

Operator identity:
The MSP user who submitted or approved a job.

Execution identity:
The app registration, certificate, managed identity, or delegated identity used to call the target API.

Audit event example:

```text
Operator: operator@msp.example
Client tenant: Contoso
Execution identity: app-client-id-for-contoso
Module: tenant-health-check v0.1.1
Target scope: Users
Targets: alex.example@contoso.com
Status: Queued
```

## Execution Modes

### Central Execution

The MSP-hosted control plane starts workers in the MSP Azure subscription. Workers use per-client credentials to call client tenant APIs.

Best fit:

- MVP.
- Reporting modules.
- Governance checks.
- Read-heavy operations.
- Low-touch deployments.

Tradeoffs:

- Cross-tenant app permissions must be carefully governed.
- Some clients may prefer client-side execution for high-risk write operations.

### Client-Side Execution Agent

A future option is to keep the control plane central but deploy a lightweight execution runtime into the client environment.

Best fit:

- Higher isolation requirements.
- Client-specific network/private endpoint access.
- Sensitive write operations.
- Clients that do not want execution credentials used from MSP runtime.

Tradeoffs:

- More deployment complexity.
- More runtime components to monitor.
- More upgrade/version management.

The MVP should use central execution while keeping contracts open for a future client-side execution agent.

## Permission Readiness

Before a module can run for a client, the control plane should compare:

- Required permissions declared by the module.
- Permissions configured on the client connection.
- Approval policy for the module/client pair.
- Allowed target scopes.

The management interface should show whether a module is ready for a client before an operator submits a job.

The MVP exposes `POST /api/readiness/check` for this comparison. Job submission uses the same readiness service, so a client that is disabled, not `Ready`, missing permissions, missing admin consent, not enabled for a module, or blocked by scope compatibility cannot run the job.

## Bootstrap Output

Target tenant bootstrap should produce a non-secret connection record that can be registered with the MSP control plane:

- Client connection ID.
- Display name.
- Target tenant ID.
- Execution app client ID.
- Target service principal object ID.
- Certificate Key Vault reference in the MSP tenant.
- Configured permissions.
- Readiness status.
- Notes describing any manual consent still required.

The bootstrap process may create the app registration automatically when run by a sufficiently privileged target tenant administrator, or it may accept manually created values from organisations that prefer pre-provisioned identities.

## Bootstrap Helper

The repository includes `scripts/new-client-connection-bootstrap.ps1` to generate the JSON record used by the control plane.

Client connection records can be registered, reviewed, and updated through the management UI or API. This allows an MSP operator to start with a `Draft` or `PendingConsent` record, then update the execution app ID, service principal ID, certificate reference, configured permissions, and readiness notes as target tenant bootstrap progresses.

Manual/pre-created app registration mode:

```powershell
.\scripts\new-client-connection-bootstrap.ps1 `
  -ClientConnectionId "client-contoso" `
  -DisplayName "Contoso" `
  -TenantId "<target-tenant-id>" `
  -ExecutionAppClientId "<target-app-client-id>" `
  -ServicePrincipalObjectId "<target-service-principal-object-id>" `
  -OutputPath ".\samples\client-connection-contoso.json"
```

This path is useful when the target tenant administrator creates the app registration manually and supplies the resulting IDs to the MSP implementor.

Automated target app registration mode:

```powershell
az login --tenant <target-tenant-id> --use-device-code --allow-no-subscriptions

.\scripts\new-client-connection-bootstrap.ps1 `
  -ClientConnectionId "client-contoso" `
  -DisplayName "Contoso" `
  -TenantId "<target-tenant-id>" `
  -CreateAppRegistration `
  -OutputPath ".\samples\client-connection-contoso.json"
```

The target tenant does not need an Azure subscription for this step. The bootstrap uses Entra ID only.

The automated path creates the target tenant application and service principal metadata, and configures the Microsoft Graph application permissions declared through `-GraphApplicationPermissions`. Certificate credential creation, Key Vault import, and admin consent remain explicit follow-up steps until the production bootstrap pack is expanded.

Grant target tenant admin consent after the required permissions are on the app registration:

```powershell
az ad app permission admin-consent --id "<target-app-client-id>"
```

When `-OutputPath` is supplied, the helper also writes a `.next-steps.md` file beside the generated JSON. This companion file lists the remaining target tenant and MSP Key Vault actions required before the connection should be marked `Ready`.

## Runtime Token Broker

The Container Apps execution provider asks the execution token broker for runtime environment values before it starts a module job.

For modules that declare Microsoft Graph permissions, the broker:

- loads the client connection
- resolves the configured Key Vault certificate reference
- downloads the certificate through the control plane managed identity
- requests a client-credential token for the client tenant using `https://graph.microsoft.com/.default`
- injects the short-lived token into the module worker as `GRAPH_ACCESS_TOKEN`

Provisioning remains outside the execution path. The broker does not create app registrations, grant permissions, upload certificates, or mark client connections as ready. It only mints a token when the connection is already configured.

Runtime-resolvable certificate references are:

- a Key Vault certificate name, for example `client-contoso-graph`
- `kv://certificates/client-contoso-graph`
- a Key Vault certificate URI such as `https://<vault>.vault.azure.net/certificates/client-contoso-graph/<version>`

Logical placeholders such as `kv://clients/client-contoso/graph-certificate` are useful for early design notes, but they are not valid for runtime token minting.

## Provisioning Plan API

The control plane exposes `POST /api/provisioning/plan` to generate an operator-facing plan for enabling a module against a client connection.

Request:

```json
{
  "clientConnectionId": "client-contoso",
  "moduleId": "msp-account-management-report",
  "moduleVersion": "0.1.2"
}
```

The response includes:

- client and module identifiers
- required module permissions
- readiness blocking issues
- recommended certificate reference
- ordered provisioning steps with owner and status

This endpoint is intentionally advisory. It does not create app registrations, grant Graph permissions, upload certificates, or change client readiness. Those actions belong to the access provisioning workflow and should require explicit operator/admin action.

## Certificate Provisioning Helper

The repository includes `scripts/new-client-execution-certificate.ps1` for the MSP-side certificate step.

The helper:

- creates a local exportable self-signed certificate
- exports a temporary PFX and password under `.work/client-certificates`
- adds the public certificate to the target tenant app registration
- imports the PFX into the MSP Key Vault
- returns the runtime certificate reference, for example `kv://certificates/client-contoso-graph`
- removes the temporary certificate from the current user's certificate store

The `.work` folder and `*.pfx` files are gitignored. The temporary PFX and password file should still be deleted after import.

Example:

```powershell
.\scripts\new-client-execution-certificate.ps1 `
  -ClientConnectionId "client-contoso" `
  -TargetTenantId "<target-tenant-id>" `
  -ExecutionAppClientId "<target-app-client-id>" `
  -KeyVaultName "<msp-key-vault-name>"
```

Required privileges:

- target tenant permission to update the execution app registration credentials
- MSP Azure permission to import certificates into the control plane Key Vault

Because the Key Vault uses Azure RBAC, the operator running the helper needs a Key Vault data-plane role such as `Key Vault Certificates Officer`. The Function App managed identity needs certificate/secret read roles so it can download the private key at execution time. Terraform grants the Function App identity `Key Vault Certificate User` and `Key Vault Secrets User`.
