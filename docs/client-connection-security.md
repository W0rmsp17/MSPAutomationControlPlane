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
Operator: chris@msp.example
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

Manual/pre-created app registration mode:

```powershell
.\scripts\new-client-connection-bootstrap.ps1 `
  -ClientConnectionId "client-contoso" `
  -DisplayName "Contoso" `
  -TenantId "<target-tenant-id>" `
  -OutputPath ".\samples\client-connection-contoso.json"
```

Automated target app registration mode:

```powershell
az login --tenant <target-tenant-id>

.\scripts\new-client-connection-bootstrap.ps1 `
  -ClientConnectionId "client-contoso" `
  -DisplayName "Contoso" `
  -TenantId "<target-tenant-id>" `
  -CreateAppRegistration `
  -OutputPath ".\samples\client-connection-contoso.json"
```

The automated path creates the target tenant application and service principal metadata only. Certificate credential creation, Key Vault import, and admin consent remain explicit follow-up steps until the production bootstrap pack is expanded.
