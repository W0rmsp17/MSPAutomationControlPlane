# Tenant Health Check Module

This is the first sample snap-in module for the MSP Automation Control Plane.

It proves the module execution contract without calling a live tenant API yet. The module reads the standard job input document, validates that it can parse the target scope, and writes a structured job output document.

## Runtime Contract

Supported input environment variables:

```text
CONTROL_PLANE_INPUT_PATH=/work/input/job.json
CONTROL_PLANE_OUTPUT_PATH=/work/output/result.json
CONTROL_PLANE_JOB_INPUT_BASE64=<base64 encoded job input JSON>
CONTROL_PLANE_OUTPUT_BLOB_URI=https://<account>.blob.core.windows.net/artifacts/jobs/<job-id>/result.json
```

For local file-based execution, use `CONTROL_PLANE_INPUT_PATH` and `CONTROL_PLANE_OUTPUT_PATH`.
For Container Apps execution, the control plane can pass `CONTROL_PLANE_JOB_INPUT_BASE64` and `CONTROL_PLANE_OUTPUT_BLOB_URI`. The module uploads result JSON to the output blob using managed identity. When `CONTROL_PLANE_OUTPUT_PATH` is not set, the module also writes the result JSON to stdout.

Exit codes:

- `0`: output written successfully.
- `2`: input could not be read or parsed.
- `3`: output could not be written.

## Local Run

```powershell
$env:CONTROL_PLANE_INPUT_PATH = "$PWD\samples\job-input.json"
$env:CONTROL_PLANE_OUTPUT_PATH = "$PWD\.out\result.json"
dotnet run --project .\src\TenantHealthCheck\TenantHealthCheck.csproj
```

## Container Build

```powershell
docker build -t tenant-health-check:0.1.1 .
```

The manifest image reference is intended for GitHub Container Registry once publish is enabled:

```text
ghcr.io/w0rmsp17/mspautomationcontrolplane/tenant-health-check:0.1.1
```
