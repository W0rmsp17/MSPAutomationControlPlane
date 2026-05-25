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
  "tenantContext": {
    "clientId": "client-contoso",
    "tenantId": "11111111-1111-1111-1111-111111111111",
    "tenantName": "Contoso"
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

Secrets should be references. The worker or control plane identity should resolve them through Key Vault permissions.

## Runtime Environment

The worker should provide these environment variables where practical:

```text
CONTROL_PLANE_JOB_ID=job-20260525-000001
CONTROL_PLANE_MODULE_ID=tenant-health-check
CONTROL_PLANE_INPUT_PATH=/work/input/job.json
CONTROL_PLANE_OUTPUT_PATH=/work/output/result.json
CONTROL_PLANE_CALLBACK_URL=https://control.example.com/api/jobs/job-20260525-000001/callback
```

The module should read the input file, execute, write the output file, and optionally call the callback URL.

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

