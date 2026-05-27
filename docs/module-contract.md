# Module Contract

Snap-in modules communicate with the control plane through a stable contract. The contract should be versioned so modules can evolve without breaking the platform.

## Module Manifest

Each module should publish a manifest similar to this:

```json
{
  "schemaVersion": "1.0",
  "id": "tenant-health-check",
  "name": "Tenant Health Check",
  "version": "0.1.0",
  "description": "Validates access and returns basic tenant health information.",
  "image": "ghcr.io/example/tenant-health-check:0.1.0",
  "runtime": "container-apps-job",
  "timeoutSeconds": 900,
  "concurrency": 1,
  "approvalRequired": false,
  "supportedScopes": [
    "Tenant",
    "Users"
  ],
  "parametersSchema": {
    "type": "object",
    "properties": {
      "includeUsers": {
        "type": "boolean",
        "default": false
      }
    },
    "required": []
  },
  "requiredPermissions": [
    {
      "provider": "MicrosoftGraph",
      "permission": "Organization.Read.All",
      "type": "Application"
    }
  ],
  "secretReferences": [],
  "outputsSchema": {
    "type": "object",
    "required": ["status", "summary", "findings"]
  }
}
```

## Job Input

The control plane should pass a standard input document to the module.

```json
{
  "schemaVersion": "1.0",
  "jobId": "job-20260525-000001",
  "moduleId": "tenant-health-check",
  "moduleVersion": "0.1.0",
  "requestedBy": {
    "userId": "00000000-0000-0000-0000-000000000000",
    "displayName": "Operator Name",
    "upn": "operator@example.com"
  },
  "clientConnectionId": "client-contoso",
  "targetScope": {
    "type": "Users",
    "mode": "Selected",
    "targets": [
      {
        "id": "22222222-2222-2222-2222-222222222222",
        "displayName": "Alex Example",
        "upn": "alex.example@contoso.com"
      }
    ]
  },
  "parameters": {
    "includeUsers": false
  },
  "secretReferences": {
    "graphCredential": "kv://client-contoso/graph-credential"
  },
  "callback": {
    "url": "https://control.example.com/api/jobs/job-20260525-000001/callback",
    "method": "POST"
  }
}
```

The control plane resolves `clientConnectionId` to tenant context, execution identity, enabled modules, allowed scopes, and credential references. Job submitters should not provide raw tenant execution details directly.

Secrets should be references. The worker or control plane identity should resolve them through Key Vault permissions.

## Target Scopes

Target scope is a first-class part of the contract. It tells the module what object set it is expected to work against.

Initial scope types:

- `Tenant`
- `Users`
- `Groups`
- `Devices`
- `Subscriptions`
- `ResourceGroups`
- `Custom`

Initial scope modes:

- `All`
- `Selected`
- `Filtered`

The control plane should validate that a submitted job uses a scope supported by the module manifest.

## Runtime Environment

The worker should provide these environment variables where practical:

```text
CONTROL_PLANE_JOB_ID=job-20260525-000001
CONTROL_PLANE_MODULE_ID=tenant-health-check
CONTROL_PLANE_MODULE_VERSION=0.1.0
CONTROL_PLANE_CLIENT_CONNECTION_ID=client-contoso
CONTROL_PLANE_REQUESTED_BY=operator@example.com
CONTROL_PLANE_JOB_INPUT_BASE64=<base64 encoded job input JSON>
CONTROL_PLANE_OUTPUT_BLOB_URI=https://<storage-account>.blob.core.windows.net/artifacts/jobs/<job-id>/result.json?<job-scoped-sas>
CONTROL_PLANE_RUNTIME_TOKEN_URL=https://<control-plane-api>/api/execution/tokens/graph
CONTROL_PLANE_RUNTIME_TOKEN=<short-lived job-scoped broker token>
```

Container Apps executions receive `CONTROL_PLANE_JOB_INPUT_BASE64` and `CONTROL_PLANE_OUTPUT_BLOB_URI`.
The output URI is scoped to one job result blob and expires after the module timeout window.
Local module executions may use `CONTROL_PLANE_INPUT_PATH` and `CONTROL_PLANE_OUTPUT_PATH` instead.

Modules that need Microsoft Graph should prefer the runtime broker exchange over direct Graph token injection:

1. Read `CONTROL_PLANE_RUNTIME_TOKEN_URL`.
2. Send `POST` with `Authorization: Bearer <CONTROL_PLANE_RUNTIME_TOKEN>`.
3. Use the returned `accessToken` only in memory for the live Graph calls required by that job.

`GRAPH_ACCESS_TOKEN` remains acceptable for local development and backward-compatible module testing, but deployed control plane execution should use the broker exchange so Graph bearer tokens are not written into Container Apps execution metadata.

## Artifact Retrieval

Operators and downstream systems should retrieve completed module artifacts through the control plane API, not directly through worker-scoped storage SAS values.

```text
GET /api/jobs/{jobId}/artifacts
GET /api/jobs/{jobId}/artifacts/result
```

The first endpoint returns generic artifact descriptors. The second returns the raw structured module result JSON. This keeps the control plane module-agnostic while giving BYOAI, webhook, export, and dashboard consumers a stable data access path.

Downstream consumers can generate derived artifacts from raw module output. Derived artifacts must reference their source job artifact and should not replace the raw module result as the source of truth. See `docs/ai-data-consumer.md`.

## Job Output

The module should return structured output.

```json
{
  "schemaVersion": "1.0",
  "jobId": "job-20260525-000001",
  "status": "Succeeded",
  "summary": "Tenant health check completed.",
  "findings": [
    {
      "severity": "Info",
      "code": "GRAPH_ACCESS_OK",
      "title": "Graph access validated",
      "detail": "The module successfully queried Microsoft Graph organization data."
    }
  ],
  "metrics": {
    "durationSeconds": 18,
    "apiCalls": 3
  },
  "artifacts": [
    {
      "name": "tenant-health-summary.json",
      "type": "application/json",
      "uri": "https://storage.example.com/artifacts/job-20260525-000001/tenant-health-summary.json"
    }
  ]
}
```

Allowed status values:

- `Succeeded`
- `Failed`
- `Partial`
- `Cancelled`

## Exit Codes

Recommended exit code behavior:

- `0`: Module completed and output was written.
- `1`: Module failed due to expected validation or runtime error.
- `2`: Module could not read input.
- `3`: Module could not write output.
- `10`: Authentication or authorization failure.

The output status remains the source of truth where possible. Exit code is a fallback signal for the worker platform.

## Versioning

Rules:

- Manifest schema changes should be versioned.
- Module IDs should remain stable.
- Breaking parameter changes should increment the module major version.
- The control plane should keep a record of the manifest version used for each job.
