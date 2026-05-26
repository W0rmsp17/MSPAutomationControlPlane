# Module CI/CD Model

Snap-in modules can be introduced through two supported intake paths:

1. Manual manifest registration.
2. CI/CD-produced module artifacts.

Both paths converge at the same platform boundary:

```text
validated module manifest -> registered module -> job execution
```

The control plane should not pull arbitrary source code from Git and execute it. Git is the source-of-truth and collaboration system, not the runtime trust boundary.

## Manual Manifest Path

The manual path is useful for labs, demos, early module development, and internal one-off modules.

```text
operator pastes manifest JSON
  -> control plane validates manifest
  -> control plane stores module registration
  -> module can be enabled for client connections
```

This path uses the existing `POST /api/modules` endpoint.

## CI/CD Artifact Path

The CI/CD path is the preferred production and portfolio story.

```text
module source repo
  -> tests
  -> build container image
  -> publish immutable image tag or digest
  -> validate/publish module manifest
  -> operator registers manifest with control plane
```

The control plane consumes the manifest and the built artifact reference. It does not clone the source repository or run unreviewed source code.

## Recommended Module Repository Shape

For in-repo sample modules:

```text
modules/
  tenant-health-check/
    src/
    tests/
    Dockerfile
    module.manifest.json
    README.md
```

For external module repositories, keep the same shape so modules can move between repositories without changing the platform contract.

## GitHub Actions Responsibilities

The module CI workflow should:

- Restore dependencies.
- Run tests.
- Validate `module.manifest.json`.
- Build the module container image.
- Publish the image to an allowed registry such as GitHub Container Registry.
- Tag the image using the module version.
- Optionally publish a manifest artifact that references the built image tag or digest.

The first workflow can build only. Publishing to GHCR can be enabled once repository package permissions and naming are final.

The in-repo `tenant-health-check` workflow publishes when either:

- A tag matching `tenant-health-check-v*` is pushed.
- The workflow is run manually through GitHub Actions.

The current image tag is:

```text
ghcr.io/w0rmsp17/mspautomationcontrolplane/tenant-health-check:0.1.1
```

After publish, the workflow uploads the module manifest and image digest as an artifact. The control plane registers the manifest image reference; future hardening should prefer digest-pinned image references.

For the simplest lab test, make the GHCR package public after the first publish so Container Apps can pull the module image without registry credentials. For a private registry, set the Terraform variables:

```hcl
container_registry_server   = "ghcr.io"
container_registry_username = "<github-username>"
container_registry_password = "<classic-pat-or-fine-grained-token-with-package-read>"
```

The password is marked sensitive and is stored as a Container Apps Job secret.

## Security Boundary

The design intentionally separates responsibilities:

- Git stores source.
- CI verifies and builds.
- Container registry stores immutable runtime artifacts.
- Manifest validation controls module intake.
- Container Apps Jobs execute modules in an isolated runtime.
- The control plane owns orchestration, audit, state, and identity.

This avoids making the MSP management plane a build server or arbitrary-code execution service.

## Future Enhancements

Later hardening can add:

- Image digest pinning.
- Image signing.
- Trusted publisher metadata.
- Vulnerability scan status.
- Per-module approval policies.
- Registry-specific allow-lists per environment.
- Manifest import from a Git raw URL, limited to manifest discovery only.
