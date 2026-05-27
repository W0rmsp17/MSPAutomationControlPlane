# AI and Data Consumer Connector Model

AI is a downstream consumer of module artifacts. It is not part of module execution and it is not a replacement for the raw module result.

```text
Module job completes
  -> raw result artifact is stored
  -> data consumer connector reads the artifact
  -> derived output is generated
  -> derived artifact is stored or delivered
```

The raw module artifact remains the source of truth. AI output is derived analysis.

## Goals

- Keep the control plane module-agnostic.
- Let MSPs attach optional AI, reporting, webhook, dashboard, or export consumers.
- Avoid granting AI systems direct Microsoft Graph access.
- Make derived outputs auditable and traceable to the source job artifact.
- Keep prompt templates and generated outputs versioned.

## Non-Goals

- AI should not call Microsoft Graph directly.
- AI should not receive client credentials, broker tokens, SAS URLs, or Key Vault references.
- AI should not mutate client tenants.
- AI should not replace readiness, policy, or approval checks.
- The management UI should not become a report editor.

## Connector Types

Initial connector categories:

- `AI`
- `Webhook`
- `StorageExport`
- `PowerBI`
- `EmailRenderer`

The AI connector is one implementation of the generic data consumer pattern.

## AI Connector Definition

Example connector definition:

```json
{
  "id": "connector-account-summary-ai",
  "displayName": "Account Manager AI Summary",
  "type": "AI",
  "enabled": true,
  "provider": "OpenAI",
  "model": "configured-outside-source",
  "promptTemplateId": "account-management-summary-v1",
  "input": {
    "artifactKind": "module-output",
    "contentType": "application/json"
  },
  "output": {
    "artifactKind": "derived-ai-summary",
    "contentType": "application/json"
  },
  "policy": {
    "requiresManualRun": true,
    "storePrompt": true,
    "storeResponse": true,
    "allowPersonalData": true,
    "maxInputBytes": 262144
  }
}
```

Provider credentials should be stored as Key Vault references or managed identities, depending on the provider. They should not be embedded in connector definitions.

## Prompt Templates

Prompt templates should be versioned records controlled by the MSP.

Example:

```json
{
  "id": "account-management-summary-v1",
  "version": "1.0.0",
  "displayName": "Account Management Summary",
  "inputSchema": "msp-account-management-report-output-v1",
  "outputSchema": "account-management-ai-summary-v1",
  "instructions": [
    "Summarise the customer licensing posture for an MSP account manager.",
    "Use only facts present in the input artifact.",
    "Separate confirmed findings from inferred recommendations.",
    "Return JSON matching the requested output schema."
  ]
}
```

Prompt templates should avoid tenant-specific hard-coding. Client-specific style or commercial context can be supplied as connector parameters later, but should be treated as configuration, not module code.

## Derived Artifact Shape

Derived artifacts should be stored separately from the source artifact.

```json
{
  "schemaVersion": "1.0",
  "id": "derived-20260527-000001",
  "jobId": "job-20260527112313-example",
  "sourceArtifact": {
    "kind": "module-output",
    "path": "jobs/job-20260527112313-example/artifacts/result",
    "contentHash": "sha256-placeholder"
  },
  "connector": {
    "id": "connector-account-summary-ai",
    "type": "AI",
    "provider": "OpenAI"
  },
  "promptTemplate": {
    "id": "account-management-summary-v1",
    "version": "1.0.0"
  },
  "createdAt": "2026-05-27T12:00:00Z",
  "createdBy": "operator@msp.example",
  "classification": "DerivedCustomerConfidential",
  "output": {
    "summary": "The tenant has available E5 Developer capacity and several disabled users retaining licenses.",
    "risks": [],
    "recommendations": [],
    "chartData": {}
  }
}
```

The `sourceArtifact.contentHash` allows an operator or downstream system to prove which raw artifact was used to generate the derived output.

## Proposed API Shape

Manual processing should come first.

```text
POST /api/data-consumers
GET  /api/data-consumers
POST /api/jobs/{jobId}/artifacts/{artifactName}/process
GET  /api/jobs/{jobId}/derived-artifacts
GET  /api/jobs/{jobId}/derived-artifacts/{derivedArtifactId}
```

Example process request:

```json
{
  "connectorId": "connector-account-summary-ai",
  "promptTemplateId": "account-management-summary-v1",
  "parameters": {
    "audience": "MSP account manager",
    "tone": "concise"
  }
}
```

The process endpoint should:

1. Authorize the operator.
2. Load the source artifact through the control plane artifact service.
3. Check connector policy.
4. Check data classification compatibility.
5. Build the provider request.
6. Store the derived artifact.
7. Write audit events.

## Security Controls

Required controls:

- Connector registration requires an authorized operator.
- Provider credentials are Key Vault references or managed identity configuration.
- Source artifact access goes through the control plane API.
- AI connector receives only the selected artifact content and approved connector parameters.
- Derived artifacts are labelled as derived analysis.
- Prompt template ID/version is stored with the derived artifact.
- Every processing run writes audit events.

Recommended controls:

- max input size per connector
- allow/deny personal data by connector
- allow/deny client connections by connector
- prompt injection resistant templates
- optional redaction or minimization before provider call
- no automatic processing until manual processing is proven

## Account Report Test Case

The account-management report is the first good test case because it already emits structured metrics, findings, recommendations, and chart-friendly data.

Test flow:

```text
GET /api/jobs/{jobId}/artifacts/result
  -> AI connector consumes raw JSON
  -> AI returns account-manager summary JSON
  -> derived artifact is stored under the source job
```

The AI connector should produce:

- executive summary
- notable licensing risks
- recommended account-manager talking points
- chart-ready license usage data
- explicit assumptions or missing data

The generated output should not be treated as authoritative tenant data. It is a presentation and interpretation layer over the raw module output.

## Future Automation

Once manual processing is proven, data consumers can be attached to triggers:

```text
Scheduled trigger
  -> run module
  -> collect result artifact
  -> run AI connector
  -> deliver derived artifact through notification connector
```

This should still use the same artifact and derived-artifact contracts. Automation should not bypass connector policy, data classification checks, or audit logging.
