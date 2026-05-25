# Control Plane Services

The value of the control plane is not just that it can run containers. Its real purpose is to remove the repeated platform work that every automation would otherwise need to build for itself.

## Services Provided To Snap-Ins

### Tenant Selection

The platform should let an operator choose where a module runs:

- Single tenant.
- Multiple selected tenants.
- Tenant group.
- All enabled tenants.

Tenant context should be passed into the job envelope so the module always knows which client it is acting against.

### Target Scope Selection

Modules should be able to run against different object scopes. The control plane owns the picker and validation experience.

Supported scope types should include:

- Tenant.
- Users.
- Groups.
- Devices.
- Azure subscriptions.
- Resource groups.
- Custom object lists.

Examples:

- A license report may run at tenant scope.
- A password reset helper may run against one selected user.
- A cleanup task may run against multiple users.
- A governance report may run against one or more subscriptions.

### Input Collection

Modules should declare their required parameters in the manifest. The control plane can then generate the operator input form from the parameter schema.

This prevents each module from needing to build its own UI for basic inputs.

### Identity And Secret Brokering

Modules should not hard-code credentials or own their own secret store. They should request named credential references through the job contract.

The control plane should provide:

- Key Vault secret references.
- Managed identity access where practical.
- Per-client credential separation.
- Permission checks before job execution.
- Clear missing-permission feedback.

### Permission Declaration

Each module should declare required permissions. The control plane can use this metadata to show what is needed before the module is enabled for a client.

Examples:

- Microsoft Graph application permissions.
- Azure RBAC roles.
- Exchange Online permissions if a future module requires them.
- Defender or cost management read access.

### Approval And Risk Controls

Modules should be able to declare whether they require approval. Later versions can support:

- Approval required.
- Dry-run required before execution.
- Change-window required.
- Maximum target count.
- High-risk permission warning.

### Job Orchestration

The control plane owns:

- Job submission.
- Queueing.
- Retry policy.
- Cancellation.
- Status tracking.
- Run history.
- Dead-letter handling.
- Worker callbacks.

Modules should not need to implement platform-level orchestration.

### Output Presentation

Modules should return structured results. The control plane should understand common result types:

- Summary.
- Findings.
- Metrics.
- Tables.
- Artifacts.
- Logs.

This lets different modules produce a consistent operator experience.

### Audit

Every job should record:

- Who requested it.
- Which tenant it targeted.
- Which objects it targeted.
- Which module and version ran.
- What inputs were provided.
- Whether approval was required and who approved it.
- Final status and output location.

## Module Author Responsibilities

A module author should provide:

- Module manifest.
- Container image.
- Parameter schema.
- Supported target scopes.
- Required permission metadata.
- Execution logic.
- Structured output.

They should not need to build tenant selection, user selection, approval workflow, secret storage, queue handling, run history, or audit.

