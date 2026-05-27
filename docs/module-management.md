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

Repository import flow:

```text
POST /api/modules/import
  -> Reject moving branch refs unless explicitly allowed
  -> Fetch module.manifest.json from a pinned repository ref or manifest URL
  -> Validate module manifest
  -> Check module ID and version uniqueness
  -> Store registered module
  -> Return module registration result
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
- Repository import using a manifest path and immutable ref, such as a release tag.
- CI/CD-produced artifacts where GitHub Actions or another pipeline tests module code, builds a container image, and publishes a manifest referencing that image.

Both paths converge on `POST /api/modules`. The control plane validates the manifest, stores the registration, and later schedules the referenced image. It does not build or execute raw Git source.

For the released account-management report module, see `samples/import-account-management-report-module.json`.
To smoke test the import endpoint against a running local or deployed API, use `scripts/test-module-import.ps1`.

Repository import does not mean the MSP tenant blindly trusts Git.
The control plane should trust only a validated, pinned, operator-approved module registration.
Imports from moving refs such as `main`, `master`, `develop`, `dev`, `trunk`, or `HEAD` are rejected by default.
Use release tags or commit SHAs for repeatable imports.
The MVP importer fetches public raw manifests only. Direct `manifestUrl` imports are limited to HTTPS `raw.githubusercontent.com` URLs and moving refs are rejected by default. Private repository import should be added through a trusted GitHub App or OIDC-backed workflow, not by passing long-lived GitHub tokens in ad hoc operator requests.

See [Module CI/CD Model](./module-ci-cd.md) for the full pipeline model.

## Module Trust Model

The control plane trusts release artifacts, not repository source code.

Trust boundary:

```text
Git repository source
  -> CI/CD tests and builds
  -> Versioned container image
  -> Versioned module manifest
  -> Control-plane validation
  -> Operator approval and client enablement
  -> Runtime execution
```

The controller should not clone arbitrary source, build code, or execute files from Git. Git is a discovery and release metadata source only.

Current supported trust controls:

- Import manifests from immutable release tags or commit SHAs.
- Reject moving refs by default.
- Validate the module manifest before registration.
- Require module ID and version uniqueness.
- Allow only configured container registry hostnames.
- Require explicit client connection readiness and module enablement before job submission.
- Store module registrations and job state in the control plane.

Recommended production hardening:

- Record the image digest at registration time and execute by digest rather than mutable tag.
- Add a GitHub App for private manifest imports with short-lived installation tokens.
- Add an OIDC-based CI registration path where a trusted module pipeline calls the control-plane API after publishing.
- Keep manual manifest registration as a fallback for disconnected or restricted environments.
- Prefer public images for public demo modules and managed private registry credentials for private MSP modules.
- Store private registry credentials in Key Vault or Terraform-managed secrets, not in ad hoc scripts.

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
- Repository import refs are immutable release tags or commit SHAs unless moving refs are explicitly allowed for development.
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
