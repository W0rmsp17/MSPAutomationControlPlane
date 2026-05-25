# Module Management

The management interface is the entry point for adding snap-in automation modules to the control plane.

This turns modules into registered products inside the platform. Operators should be able to add, validate, enable, run, and review modules without redeploying the control plane.

## MVP Approach

The MVP should start with API-based module registration before building the full UI.

Initial API flow:

```text
POST /api/modules
  -> Validate module manifest
  -> Check module ID and version uniqueness
  -> Store registered module
  -> Return module registration result

GET /api/modules
  -> Return registered module catalog

GET /api/modules/{moduleId}
  -> Return module details, supported scopes, required permissions, and latest run summary
```

The UI can later call the same APIs.

## Future Management UI

The management interface should eventually provide:

- Registered modules.
- Add module.
- Validate manifest.
- Enable or disable module per tenant.
- View required permissions.
- View supported target scopes.
- View run history.
- View current module health.
- View latest version and image reference.

Suggested navigation:

```text
Dashboard
Tenants
Modules
Jobs
Approvals
Runtime Health
Settings
```

## Module Registration Inputs

Manual registration should accept a module manifest first. CI/CD-produced modules should still register through the same manifest validation path. Later versions can support manifest URLs or GitHub/container registry discovery, but the control plane should not clone and execute arbitrary source code.

Initial inputs:

- Module manifest JSON.
- Optional display override.
- Enabled tenants.
- Approval policy override.

The manifest provides:

- Module ID.
- Module name.
- Version.
- Container image URI.
- Runtime type.
- Supported target scopes.
- Parameter schema.
- Required permissions.
- Output schema.
- Optional documentation URL.

## Intake Paths

Supported intake paths:

- Manual JSON manifest registration for labs, demos, and early module development.
- CI/CD-produced artifacts where GitHub Actions or another pipeline tests module code, builds a container image, and publishes a manifest referencing that image.

Both paths converge on `POST /api/modules`. The control plane validates the manifest, stores the registration, and later schedules the referenced image. It does not build or execute raw Git source.

See [Module CI/CD Model](./module-ci-cd.md) for the full pipeline model.

## Validation Rules

The control plane should reject unsafe or invalid module registrations.

Initial checks:

- Manifest schema is valid.
- Module ID is present and uses lowercase letters, numbers, dots, and hyphens.
- Version is present and uses semantic version format, such as `1.0.0`.
- Module ID and version combination is unique.
- Runtime type is supported.
- Container image URI is present and includes a registry hostname.
- Container registry is allowed by platform configuration. The default allow-list is `ghcr.io,mcr.microsoft.com`.
- Supported scopes are valid.
- `parametersSchema` and `outputsSchema` are JSON objects.
- `requiredPermissions` declares at least one permission.
- Required permission entries include provider, permission, and type.
- Required permission type is `Application` or `Delegated`.
- Approval policy is valid.

Validation failures return a `400` response with an `errors` array so operators can correct all obvious manifest issues in one pass.

## Security Notes

The control plane should not blindly execute any submitted image.

MVP safety controls:

- Only allow configured container registries.
- Require explicit tenant enablement.
- Require declared permissions.
- Require approval for high-risk modules.
- Store every module registration and update as an audit event.

Later controls:

- Image signing or trusted publisher metadata.
- Vulnerability scan status.
- Per-module RBAC.
- Version pinning and rollback.
