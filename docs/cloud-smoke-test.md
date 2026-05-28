# Cloud Smoke Test

The cloud smoke test validates the deployed control plane path from an authenticated MSP operator request through asynchronous module execution.

Validated path:

1. Acquire an MSP-tenant access token for the control plane API scope.
2. Register a client connection record.
3. Register a module manifest.
4. Submit a job through the Function API.
5. Service Bus queues the job dispatch request.
6. The Service Bus-triggered Function starts the configured Container Apps Job.
7. The module container receives the standard job contract.
8. The module writes structured output to the control plane artifact container.
9. The API result collector reads the output artifact and marks the job `Succeeded`.

Expected job states:

- `Queued`: the API accepted the job and placed a dispatch message on Service Bus.
- `Running`: the dispatcher started the configured execution provider.
- `Succeeded`: the result collector found and stored the module output artifact.

Run the registration-only smoke test after infrastructure, authentication, Function App, and frontend deployment:

```powershell
.\scripts\test-cloud-smoke.ps1 -RegistrationOnly
```

The script reads deployed values from Terraform outputs and discovers the Static Web App/API auth app registration by display name. It uses the sample client connection, sample job request, and the real tenant health check module manifest by default.

The default sample client connection is public-safe placeholder data. It can validate authentication, module registration, and client registration, but it cannot execute a real job because the placeholder Key Vault certificate does not exist.

For a full execution smoke test, create untracked local files under `.work` with a real client connection and matching job request:

```powershell
.\scripts\new-full-execution-smoke-files.ps1

.\scripts\test-cloud-smoke.ps1 `
  -ClientConnectionPath ".work\smoke\client-connection-real.json" `
  -JobRequestPath ".work\smoke\submit-job-real.json"
```

The helper reads `samples/full-execution-smoke.template.json`, splits the `clientConnection` and `jobRequest` objects into local ignored files, and refuses to overwrite existing files unless `-Force` is supplied. Replace every placeholder with values from the target tenant bootstrap before running full execution.

The real client connection must reference a certificate that exists in the deployed Key Vault and has the required Microsoft Graph permissions/admin consent in the target tenant.

If Azure CLI has not yet consented to the API scope, run the scoped login once:

```powershell
az login --tenant "<msp-tenant-id>" --scope "api://<auth-app-client-id>/access_as_user"
```

The smoke test intentionally exercises the API boundary with a bearer token. A direct unauthenticated call to a protected endpoint should return an error such as `Missing bearer token.`

To test the module import endpoint separately, acquire an API token as above and run:

```powershell
.\scripts\test-module-import.ps1 -ApiBaseUrl "https://<function-hostname>/api" -AccessToken "<access-token>"
```

The import sample fetches a public raw manifest from the pinned module release. If the module repository is private, use direct manifest registration for now or add a trusted GitHub App/OIDC import path before enabling private repository imports.

## Account Report Module Smoke

The account-management report module validates the common snap-in execution path:

```text
Import pinned module release
  -> Submit job through authenticated Function API
  -> Queue dispatch through Service Bus
  -> Start Container Apps Job with module image
  -> Pass CONTROL_PLANE_JOB_INPUT_BASE64
  -> Pass CONTROL_PLANE_OUTPUT_BLOB_URI
  -> Module writes result JSON to Blob Storage
  -> API collects result and marks job Succeeded
```

When testing a private GHCR package, the Container Apps Job needs registry credentials before execution. A public repository does not automatically make the linked GHCR package public.

For production, configure private registry credentials through Terraform and Key Vault-backed deployment inputs. For public portfolio modules, make the package public in GitHub package settings so the module image is anonymously pullable.

The public package check is separate from the repository check. Anonymous pull should be able to acquire a GHCR token for the module image:

```powershell
Invoke-RestMethod `
  -Uri "https://ghcr.io/token?scope=repository%3A<owner>%2F<repo>%2F<package>%3Apull&service=ghcr.io" `
  -Method Get
```

If that request returns `UNAUTHORIZED`, the Container Apps Job will fail with `ErrImagePull` or `ImagePullBackOff` before the module code starts.

Validated result shape:

- Container Apps Job execution reached `Succeeded`.
- Control-plane job reached `Succeeded` after result collection.
- Output contained summary, findings, metrics, recommendations, license summary, user license signals, and rendered Markdown.

The first full live account-management report smoke proved:

```text
Queued -> Running -> Succeeded
```

and returned a control-plane summary similar to:

```text
Account management report scaffold generated for <client-connection-id>.
```
