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

Run the smoke test after infrastructure, authentication, Function App, and frontend deployment:

```powershell
.\scripts\test-cloud-smoke.ps1
```

The script reads deployed values from Terraform outputs and discovers the Static Web App/API auth app registration by display name. It uses the sample client connection, sample job request, and the real tenant health check module manifest by default.

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
