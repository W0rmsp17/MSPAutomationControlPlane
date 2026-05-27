const config = window.MSP_CONTROL_PLANE_CONFIG || {};
const state = {
  apiBaseUrl: localStorage.getItem("apiBaseUrl") || config.apiBaseUrl || "",
  auth: config.auth || {},
  msalClient: null,
  msalAccount: null,
  authInitialized: false,
  selectedClientId: null,
  clients: [],
  modules: [],
  jobs: [],
  dataConsumers: [],
  derivedArtifacts: [],
  notifications: [],
  auditEvents: []
};

const samples = {
  client: {
    id: "client-contoso",
    displayName: "Contoso",
    tenantId: "00000000-0000-0000-0000-000000000000",
    executionMode: "Central",
    executionAppClientId: "00000000-0000-0000-0000-000000000000",
    certificateReference: "kv://certificates/client-contoso-graph",
    servicePrincipalObjectId: "00000000-0000-0000-0000-000000000000",
    readinessStatus: "PendingConsent",
    configuredPermissions: [
      {
        provider: "MicrosoftGraph",
        permission: "Organization.Read.All",
        type: "Application",
        adminConsented: false
      }
    ],
    readinessNotes: "Replace placeholder IDs after target tenant bootstrap.",
    enabledModuleIds: ["tenant-health-check"],
    allowedScopes: ["Tenant", "Users"],
    enabled: true
  },
  module: {
    schemaVersion: "1.0",
    id: "tenant-health-check",
    name: "Tenant Health Check",
    version: "0.1.1",
    description: "Validates the control plane module registration and job contract.",
    image: "ghcr.io/example/tenant-health-check:0.1.1",
    runtime: "container-apps-job",
    timeoutSeconds: 900,
    concurrency: 1,
    approvalRequired: false,
    supportedScopes: ["Tenant", "Users"],
    parametersSchema: {
      type: "object",
      properties: {
        includeUsers: {
          type: "boolean",
          default: false
        }
      },
      required: []
    },
    requiredPermissions: [
      {
        provider: "MicrosoftGraph",
        permission: "Organization.Read.All",
        type: "Application"
      }
    ],
    outputsSchema: {
      type: "object",
      required: ["status", "summary", "findings"]
    }
  },
  notification: {
    id: "teams-ops-channel",
    displayName: "Teams Ops Channel",
    targetUrl: "https://example.invalid/webhook",
    eventTypes: ["JobSubmitted", "JobCompleted", "JobFailed"],
    enabled: true
  },
  accountReportImport: {
    source: {
      type: "git",
      repository: "https://github.com/W0rmsp17/MSPAccountManagementReport",
      ref: "v0.1.3",
      manifestPath: "module.manifest.json"
    },
    registration: {
      enabled: true,
      visibility: "Private",
      allowedClientConnectionIds: [],
      defaultRunMode: "Standard"
    },
    validation: {
      requireImageTagMatch: true,
      requirePackageValidation: true,
      allowMovingRef: false
    }
  },
  accountReportClient: {
    id: "client-account-report-demo",
    displayName: "Account Report Demo Client",
    tenantId: "00000000-0000-0000-0000-000000000000",
    executionMode: "Central",
    executionAppClientId: "00000000-0000-0000-0000-000000000000",
    certificateReference: "kv://certificates/client-account-report-demo-graph",
    servicePrincipalObjectId: "00000000-0000-0000-0000-000000000000",
    readinessStatus: "Ready",
    configuredPermissions: [
      {
        provider: "MicrosoftGraph",
        permission: "Organization.Read.All",
        type: "Application",
        adminConsented: true
      },
      {
        provider: "MicrosoftGraph",
        permission: "User.Read.All",
        type: "Application",
        adminConsented: true
      },
      {
        provider: "MicrosoftGraph",
        permission: "Directory.Read.All",
        type: "Application",
        adminConsented: true
      }
    ],
    readinessNotes: "Demo connection for account-management report execution.",
    enabledModuleIds: ["msp-account-management-report"],
    allowedScopes: ["Tenant", "Users"],
    enabled: true
  }
};

samples.job = {
  moduleId: samples.module.id,
  moduleVersion: samples.module.version,
  clientConnectionId: samples.client.id,
  targetScope: {
    type: "Users",
    mode: "Selected",
    targets: [
      {
        id: "alex.example@contoso.com",
        displayName: "Alex Example",
        userPrincipalName: "alex.example@contoso.com"
      }
    ]
  },
  parameters: {
    includeUsers: true
  }
};

samples.accountReportJob = {
  moduleId: "msp-account-management-report",
  moduleVersion: "0.1.3",
  clientConnectionId: samples.accountReportClient.id,
  targetScope: {
    type: "Tenant",
    mode: "All",
    targets: []
  },
  parameters: {
    includeInactiveUsers: true,
    includeLicenseWaste: true,
    reportFormat: "markdown"
  }
};

samples.dataConsumer = {
  id: "consumer-account-summary-template",
  displayName: "Account Summary Template Consumer",
  type: "TemplateSummary",
  enabled: true,
  provider: "ControlPlaneTemplate",
  promptTemplateId: "account-management-summary-v1",
  policy: {
    requiresManualRun: true,
    storePrompt: false,
    storeResponse: true,
    allowPersonalData: true,
    maxInputBytes: 262144
  }
};

samples.artifactProcessRequest = {
  connectorId: samples.dataConsumer.id,
  promptTemplateId: "account-management-summary-v1",
  parameters: {
    audience: "MSP account manager",
    tone: "concise"
  }
};

const el = (id) => document.getElementById(id);

function pretty(value) {
  return JSON.stringify(value, null, 2);
}

function endpoint(path) {
  return `${state.apiBaseUrl.replace(/\/$/, "")}/${path.replace(/^\//, "")}`;
}

function authConfigured() {
  return Boolean(state.auth.tenantId && state.auth.clientId && state.auth.apiScope);
}

async function initializeAuth() {
  if (!authConfigured()) {
    throw new Error("Authentication is not configured for this deployment.");
  }

  if (!window.msal) {
    throw new Error("MSAL did not load. Refresh the page and check that the Microsoft authentication script is not blocked.");
  }

  if (!state.msalClient) {
    state.msalClient = new msal.PublicClientApplication({
      auth: {
        clientId: state.auth.clientId,
        authority: `https://login.microsoftonline.com/${state.auth.tenantId}`,
        redirectUri: window.location.origin,
        navigateToLoginRequestUrl: false
      },
      cache: {
        cacheLocation: "localStorage",
        storeAuthStateInCookie: true
      }
    });
  }

  if (state.authInitialized) {
    return;
  }

  const redirectResult = await state.msalClient.handleRedirectPromise();
  state.msalAccount = redirectResult?.account || state.msalClient.getAllAccounts()[0] || null;
  state.authInitialized = true;
  if (!state.msalAccount) {
    await state.msalClient.loginRedirect({
      scopes: [state.auth.apiScope]
    });
    throw new Error("Sign-in started. Complete the Microsoft sign-in prompt to continue.");
  }
}

async function acquireAccessToken() {
  await initializeAuth();

  try {
    const result = await state.msalClient.acquireTokenSilent({
      account: state.msalAccount,
      scopes: [state.auth.apiScope]
    });
    if (!result.accessToken) {
      throw new Error("Microsoft sign-in completed but no API access token was returned.");
    }

    return result.accessToken;
  } catch (error) {
    if (error instanceof msal.InteractionRequiredAuthError) {
      await state.msalClient.acquireTokenRedirect({
        account: state.msalAccount,
        scopes: [state.auth.apiScope]
      });
      throw new Error("Additional consent is required. Complete the Microsoft prompt to continue.");
    }

    throw error;
  }
}

async function api(path, options = {}) {
  if (!state.apiBaseUrl) {
    throw new Error("Set the API base URL before calling the control plane.");
  }

  const accessToken = await acquireAccessToken();
  if (!accessToken) {
    throw new Error("A bearer token is required before calling the control plane API.");
  }

  const response = await fetch(endpoint(path), {
    ...options,
    headers: {
      "content-type": "application/json",
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
      ...(options.headers || {})
    }
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `${response.status} ${response.statusText}`);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

function setMessage(message, type = "ok") {
  const node = el("status-message");
  node.textContent = message;
  node.className = `status-message ${type}`;
  window.clearTimeout(setMessage.timeout);
  setMessage.timeout = window.setTimeout(() => node.classList.add("hidden"), 6500);
}

function setHealth(status, className) {
  const pill = el("health-pill");
  pill.textContent = status;
  pill.className = `pill ${className}`;
}

function renderList(targetId, items, renderer) {
  const target = el(targetId);
  target.innerHTML = "";
  if (!items.length) {
    const empty = document.createElement("div");
    empty.className = "empty";
    empty.textContent = "No records found.";
    target.appendChild(empty);
    return;
  }

  items.forEach((item) => target.appendChild(renderer(item)));
}

function listItem(title, meta, pillText) {
  const item = document.createElement("article");
  item.className = "list-item";

  const header = document.createElement("div");
  header.className = "list-item-header";

  const strong = document.createElement("strong");
  strong.textContent = title;
  header.appendChild(strong);

  if (pillText) {
    const pill = document.createElement("span");
    pill.className = "pill muted";
    pill.textContent = pillText;
    header.appendChild(pill);
  }

  const metaNode = document.createElement("div");
  metaNode.className = "meta";
  metaNode.textContent = meta;

  item.appendChild(header);
  item.appendChild(metaNode);
  return item;
}

function renderJobResult(job) {
  const target = el("job-result-summary");
  const reportTarget = el("job-report");
  target.innerHTML = "";
  reportTarget.textContent = "";

  if (!job?.output) {
    target.classList.add("hidden");
    return;
  }

  const output = job.output;
  const title = document.createElement("div");
  title.className = "result-title";

  const summary = document.createElement("strong");
  summary.textContent = output.summary || "Module output";
  title.appendChild(summary);

  const status = document.createElement("span");
  status.className = `pill ${String(output.status || job.status).toLowerCase() === "succeeded" ? "ok" : "warn"}`;
  status.textContent = output.status || job.status || "Unknown";
  title.appendChild(status);
  target.appendChild(title);

  if (output.metrics && Object.keys(output.metrics).length) {
    const metrics = document.createElement("div");
    metrics.className = "result-metrics";
    Object.entries(output.metrics).forEach(([key, value]) => {
      const metric = document.createElement("span");
      metric.textContent = `${key}: ${value}`;
      metrics.appendChild(metric);
    });
    target.appendChild(metrics);
  }

  if (Array.isArray(output.findings) && output.findings.length) {
    const findings = document.createElement("div");
    findings.className = "result-findings";
    output.findings.slice(0, 6).forEach((finding) => {
      const item = document.createElement("article");
      item.className = "finding";

      const findingTitle = document.createElement("strong");
      findingTitle.textContent = finding.title || finding.code || "Finding";

      const findingMeta = document.createElement("div");
      findingMeta.className = "meta";
      findingMeta.textContent = `${finding.severity || "Info"}${finding.code ? ` - ${finding.code}` : ""}`;

      const message = document.createElement("div");
      message.textContent = finding.detail || finding.message || "";

      item.appendChild(findingTitle);
      item.appendChild(findingMeta);
      item.appendChild(message);
      findings.appendChild(item);
    });
    target.appendChild(findings);
  }

  reportTarget.textContent = getRenderedReport(job) || "";
  target.classList.remove("hidden");
}

function renderModulePreview() {
  const target = el("module-preview");
  target.innerHTML = "";

  let manifest;
  try {
    manifest = JSON.parse(el("module-json").value);
  } catch {
    target.classList.add("hidden");
    return;
  }

  const requiredFields = ["id", "name", "version", "image"];
  const issues = requiredFields
    .filter((field) => !manifest[field])
    .map((field) => `Missing required field: ${field}.`);

  if (!Array.isArray(manifest.supportedScopes) || !manifest.supportedScopes.length) {
    issues.push("At least one supported scope should be declared.");
  }

  if (!manifest.parametersSchema || typeof manifest.parametersSchema !== "object") {
    issues.push("Parameters schema should be an object.");
  }

  if (!manifest.outputsSchema || typeof manifest.outputsSchema !== "object") {
    issues.push("Outputs schema should be an object.");
  }

  const rows = [
    ["Module", `${manifest.name || manifest.id || "Unnamed"} (${manifest.id || "no id"})`],
    ["Version", manifest.version || "Not set"],
    ["Runtime", manifest.runtime || "Not set"],
    ["Image", manifest.image || "Not set"],
    ["Scopes", Array.isArray(manifest.supportedScopes) ? manifest.supportedScopes.join(", ") : "Not set"],
    ["Permissions", `${manifest.requiredPermissions?.length || 0} required`],
    ["Approval", manifest.approvalRequired ? "Required" : "Not required"]
  ];

  rows.forEach(([label, value]) => {
    const row = document.createElement("div");
    row.className = "preview-row";

    const labelNode = document.createElement("span");
    labelNode.textContent = label;

    const valueNode = document.createElement("strong");
    valueNode.textContent = value;

    row.appendChild(labelNode);
    row.appendChild(valueNode);
    target.appendChild(row);
  });

  if (issues.length) {
    issues.forEach((issue) => {
      const item = document.createElement("div");
      item.className = "meta";
      item.textContent = issue;
      target.appendChild(item);
    });
  } else {
    const item = document.createElement("div");
    item.className = "meta";
    item.textContent = "Manifest preview passed basic client-side checks.";
    target.appendChild(item);
  }

  target.classList.remove("hidden");
}

function getRenderedReport(job) {
  return job?.output?.report?.renderedReport?.content || "";
}

function renderDemoResult(job) {
  const summaryTarget = el("demo-summary");
  const reportTarget = el("demo-report");
  const pill = el("demo-status-pill");
  summaryTarget.innerHTML = "";
  reportTarget.textContent = getRenderedReport(job) || "";

  if (!job) {
    summaryTarget.classList.add("hidden");
    pill.textContent = "Idle";
    pill.className = "pill muted";
    return;
  }

  pill.textContent = job.status || "Unknown";
  pill.className = `pill ${job.status === "Succeeded" ? "ok" : job.status === "Failed" ? "bad" : "warn"}`;

  const title = document.createElement("div");
  title.className = "result-title";

  const strong = document.createElement("strong");
  strong.textContent = job.output?.summary || job.id || "Job";
  title.appendChild(strong);

  const status = document.createElement("span");
  status.className = pill.className;
  status.textContent = job.status || "Unknown";
  title.appendChild(status);
  summaryTarget.appendChild(title);

  if (job.output?.metrics) {
    const metrics = document.createElement("div");
    metrics.className = "result-metrics";
    ["licenseSkuCount", "totalLicenses", "assignedLicenses", "availableLicenses", "usersChecked", "recommendationCount"].forEach((key) => {
      if (job.output.metrics[key] === undefined) {
        return;
      }

      const metric = document.createElement("span");
      metric.textContent = `${key}: ${job.output.metrics[key]}`;
      metrics.appendChild(metric);
    });
    summaryTarget.appendChild(metrics);
  }

  summaryTarget.classList.remove("hidden");
}

function renderClientPreview() {
  const target = el("client-preview");
  target.innerHTML = "";

  let client;
  try {
    client = JSON.parse(el("client-json").value);
  } catch {
    target.classList.add("hidden");
    return;
  }

  const consentedPermissions = Array.isArray(client.configuredPermissions)
    ? client.configuredPermissions.filter((permission) => permission.adminConsented).length
    : 0;
  const totalPermissions = Array.isArray(client.configuredPermissions) ? client.configuredPermissions.length : 0;

  const rows = [
    ["Client", `${client.displayName || client.id || "Unnamed"} (${client.id || "no id"})`],
    ["Tenant", client.tenantId || "Not set"],
    ["Readiness", client.readinessStatus || "Unknown"],
    ["Execution", client.executionMode || "Not set"],
    ["App client", client.executionAppClientId || "Not set"],
    ["Permissions", `${consentedPermissions}/${totalPermissions} consented`],
    ["Scopes", Array.isArray(client.allowedScopes) ? client.allowedScopes.join(", ") : "Not set"]
  ];

  rows.forEach(([label, value]) => {
    const row = document.createElement("div");
    row.className = "preview-row";

    const labelNode = document.createElement("span");
    labelNode.textContent = label;

    const valueNode = document.createElement("strong");
    valueNode.textContent = value;

    row.appendChild(labelNode);
    row.appendChild(valueNode);
    target.appendChild(row);
  });

  target.classList.remove("hidden");
}

function readinessRequestFromJobPayload(payload) {
  return {
    clientConnectionId: payload.clientConnectionId,
    moduleId: payload.moduleId,
    moduleVersion: payload.moduleVersion,
    targetScopeType: payload.targetScope?.type
  };
}

function getRegisteredModuleManifest(moduleRegistration) {
  return moduleRegistration?.manifest || moduleRegistration;
}

function selectedModuleManifest() {
  const selectedValue = el("job-module-select").value;
  return state.modules
    .map(getRegisteredModuleManifest)
    .find((manifest) => `${manifest.id}@${manifest.version}` === selectedValue);
}

function parseTargets(text, scopeType) {
  return text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .map((value) => ({
      id: value,
      displayName: value,
      ...(scopeType === "Users" ? { userPrincipalName: value } : {})
    }));
}

function composeJobRequestFromControls() {
  const moduleManifest = selectedModuleManifest();
  if (!moduleManifest) {
    throw new Error("Select a module before composing a job request.");
  }

  const clientConnectionId = el("job-client-select").value;
  if (!clientConnectionId) {
    throw new Error("Select a client before composing a job request.");
  }

  const scopeType = el("job-scope-type").value;
  const scopeMode = el("job-scope-mode").value;
  const parametersText = el("job-parameters").value.trim();
  const parameters = parametersText ? JSON.parse(parametersText) : {};

  return {
    moduleId: moduleManifest.id,
    moduleVersion: moduleManifest.version,
    clientConnectionId,
    targetScope: {
      type: scopeType,
      mode: scopeMode,
      targets: scopeMode === "Selected" ? parseTargets(el("job-targets").value, scopeType) : []
    },
    parameters
  };
}

function renderReadinessResult(result) {
  const target = el("job-readiness");
  target.innerHTML = "";

  const rows = [
    ["Status", result.isReady ? "Ready" : "Blocked"],
    ["Client", result.clientConnectionId || "Not set"],
    ["Module", `${result.moduleId || "Not set"} ${result.moduleVersion || ""}`.trim()],
    ["Scope", result.targetScopeType || "Not checked"],
    ["Permissions", `${result.matchingPermissions?.length || 0}/${result.requiredPermissions?.length || 0} matched`]
  ];

  rows.forEach(([label, value]) => {
    const row = document.createElement("div");
    row.className = "preview-row";

    const labelNode = document.createElement("span");
    labelNode.textContent = label;

    const valueNode = document.createElement("strong");
    valueNode.textContent = value;

    row.appendChild(labelNode);
    row.appendChild(valueNode);
    target.appendChild(row);
  });

  const issues = [...(result.blockingIssues || []), ...(result.warnings || [])];
  issues.forEach((issue) => {
    const item = document.createElement("div");
    item.className = "meta";
    item.textContent = issue;
    target.appendChild(item);
  });

  target.classList.remove("hidden");
}

function provisioningPlanRequestFromJobPayload(payload) {
  return {
    clientConnectionId: payload.clientConnectionId,
    moduleId: payload.moduleId,
    moduleVersion: payload.moduleVersion
  };
}

function renderProvisioningPlan(plan) {
  const target = el("job-provisioning-plan");
  target.innerHTML = "";

  const rows = [
    ["Status", plan.isExecutionReady ? "Ready" : "Action required"],
    ["Client", `${plan.clientDisplayName || plan.clientConnectionId || "Not set"} (${plan.clientConnectionId || "no id"})`],
    ["Module", `${plan.moduleId || "Not set"} ${plan.moduleVersion || ""}`.trim()],
    ["Tenant", plan.tenantId || "Not set"],
    ["Certificate", plan.recommendedCertificateReference || "Not set"]
  ];

  rows.forEach(([label, value]) => {
    const row = document.createElement("div");
    row.className = "preview-row";

    const labelNode = document.createElement("span");
    labelNode.textContent = label;

    const valueNode = document.createElement("strong");
    valueNode.textContent = value;

    row.appendChild(labelNode);
    row.appendChild(valueNode);
    target.appendChild(row);
  });

  if (Array.isArray(plan.blockingIssues) && plan.blockingIssues.length) {
    plan.blockingIssues.forEach((issue) => {
      const item = document.createElement("div");
      item.className = "provisioning-issue";
      item.textContent = issue;
      target.appendChild(item);
    });
  }

  const steps = document.createElement("div");
  steps.className = "provisioning-steps";
  (plan.steps || []).forEach((step) => {
    const item = document.createElement("article");
    item.className = "provisioning-step";

    const header = document.createElement("div");
    header.className = "provisioning-step-header";

    const title = document.createElement("strong");
    title.textContent = `${step.order}. ${step.title}`;

    const status = document.createElement("span");
    status.className = `pill ${step.status === "Complete" ? "ok" : step.status === "Blocked" ? "bad" : "warn"}`;
    status.textContent = step.status;

    header.appendChild(title);
    header.appendChild(status);

    const detail = document.createElement("div");
    detail.textContent = step.detail || "";

    const owner = document.createElement("div");
    owner.className = "meta";
    owner.textContent = step.owner ? `Owner: ${step.owner}` : "";

    item.appendChild(header);
    item.appendChild(detail);
    if (owner.textContent) {
      item.appendChild(owner);
    }

    steps.appendChild(item);
  });

  target.appendChild(steps);
  target.classList.remove("hidden");
}

function renderTimeline(targetId, events) {
  const sorted = [...events].sort((a, b) => String(b.occurredAt).localeCompare(String(a.occurredAt)));
  renderList(targetId, sorted.slice(0, 25), (event) => {
    const item = document.createElement("article");
    item.className = "timeline-item";

    const title = document.createElement("strong");
    title.textContent = event.eventType || "Event";

    const meta = document.createElement("div");
    meta.className = "meta";
    meta.textContent = `${event.occurredAt || ""} - ${event.actor || "system"}`;

    const message = document.createElement("div");
    message.textContent = event.message || "";

    item.appendChild(title);
    item.appendChild(meta);
    item.appendChild(message);
    return item;
  });
}

function renderDerivedArtifacts(items = state.derivedArtifacts) {
  renderList("derived-artifacts-list", items, (artifact) => {
    const item = listItem(
      artifact.id,
      `${artifact.jobId || ""} - ${artifact.sourceArtifactName || ""} - ${artifact.createdAt || ""}`,
      artifact.connectorId || "consumer");

    item.addEventListener("click", async () => {
      el("artifact-job-id").value = artifact.jobId || el("artifact-job-id").value;
      el("derived-artifact-id").value = artifact.id;
      try {
        const result = await api(`jobs/${encodeURIComponent(artifact.jobId)}/derived-artifacts/${encodeURIComponent(artifact.id)}`);
        el("derived-artifact-output").textContent = pretty(result);
      } catch (error) {
        setMessage(error.message, "bad");
      }
    });
    return item;
  });
}

function render() {
  el("metric-clients").textContent = state.clients.length;
  el("metric-modules").textContent = state.modules.length;
  el("metric-notifications").textContent = state.notifications.length;
  el("metric-audit").textContent = state.jobs.length;

  renderList("clients-list", state.clients, (client) => {
    const item = listItem(
      client.displayName || client.id,
      `${client.tenantId || ""} - ${client.executionMode || ""} - ${client.readinessStatus || "Unknown"}`,
      client.enabled ? "Enabled" : "Disabled");

    item.addEventListener("click", () => {
      state.selectedClientId = client.id;
      el("client-json").value = pretty(client);
      renderClientPreview();
    });
    return item;
  });

  renderList("modules-list", state.modules, (module) => {
    const manifest = getRegisteredModuleManifest(module);
    const item = listItem(manifest.name || manifest.id, `${manifest.id || ""} - ${manifest.version || ""}`, manifest.runtime || "module");
    item.addEventListener("click", () => {
      el("module-json").value = pretty(manifest);
      renderModulePreview();
    });
    return item;
  });

  renderList("notifications-list", state.notifications, (subscription) =>
    listItem(subscription.displayName || subscription.id, subscription.targetUrl || "", subscription.enabled ? "Enabled" : "Disabled"));

  renderList("data-consumers-list", state.dataConsumers, (consumer) => {
    const item = listItem(
      consumer.displayName || consumer.id,
      `${consumer.provider || ""} - ${consumer.promptTemplateId || ""}`,
      consumer.enabled ? "Enabled" : "Disabled");

    item.addEventListener("click", () => {
      el("data-consumer-json").value = pretty(consumer);
    });
    return item;
  });
  renderDerivedArtifacts();

  renderList("jobs-list", state.jobs, (job) => {
    const item = listItem(
      job.id,
      `${job.moduleId || ""} - ${job.tenantContext?.tenantName || job.tenantContext?.clientId || ""} - ${job.createdAt || ""}`,
      job.status || "Unknown");

    item.addEventListener("click", () => {
      el("job-id").value = job.id;
      el("artifact-job-id").value = job.id;
      renderJobResult(job);
      el("job-output").textContent = pretty(job);
    });
    return item;
  });

  renderTimeline("audit-list", state.auditEvents);
  renderTimeline("recent-activity", state.auditEvents);
  renderJobComposerOptions();
}

function renderJobComposerOptions() {
  const clientSelect = el("job-client-select");
  const moduleSelect = el("job-module-select");
  const selectedClient = clientSelect.value;
  const selectedModule = moduleSelect.value;

  clientSelect.innerHTML = "";
  state.clients.forEach((client) => {
    const option = document.createElement("option");
    option.value = client.id;
    option.textContent = `${client.displayName || client.id} (${client.readinessStatus || "Unknown"})`;
    clientSelect.appendChild(option);
  });

  moduleSelect.innerHTML = "";
  state.modules.map(getRegisteredModuleManifest).forEach((manifest) => {
    const option = document.createElement("option");
    option.value = `${manifest.id}@${manifest.version}`;
    option.textContent = `${manifest.name || manifest.id} (${manifest.version})`;
    moduleSelect.appendChild(option);
  });

  if ([...clientSelect.options].some((option) => option.value === selectedClient)) {
    clientSelect.value = selectedClient;
  }

  if ([...moduleSelect.options].some((option) => option.value === selectedModule)) {
    moduleSelect.value = selectedModule;
  }
}

async function refreshAll() {
  try {
    setHealth("Checking", "muted");
    await api("health");
    setHealth("Healthy", "ok");

    const [clients, modules, jobs, dataConsumers, notifications, auditEvents] = await Promise.all([
      api("client-connections"),
      api("modules"),
      api("jobs"),
      api("data-consumers"),
      api("notification-subscriptions"),
      api("audit-events")
    ]);

    state.clients = clients;
    state.modules = modules;
    state.jobs = jobs;
    state.dataConsumers = dataConsumers;
    state.notifications = notifications;
    state.auditEvents = auditEvents;
    render();
  } catch (error) {
    setHealth("Attention", "warn");
    setMessage(error.message, "warn");
  }
}

async function submitJsonForm(textareaId, path) {
  const payload = JSON.parse(el(textareaId).value);
  const result = await api(path, {
    method: "POST",
    body: JSON.stringify(payload)
  });
  await refreshAll();
  return result;
}

function errorText(error) {
  return error?.message || String(error);
}

async function importModuleRelease(payload) {
  try {
    return await api("modules/import", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  } catch (error) {
    if (errorText(error).includes("already registered")) {
      return null;
    }

    throw error;
  }
}

async function registerOrUpdateClient(payload) {
  try {
    return await api("client-connections", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  } catch (error) {
    if (!errorText(error).includes("already registered")) {
      throw error;
    }

    return api(`client-connections/${encodeURIComponent(payload.id)}`, {
      method: "PUT",
      body: JSON.stringify(payload)
    });
  }
}

function wireNavigation() {
  document.querySelectorAll(".nav-item").forEach((button) => {
    button.addEventListener("click", () => {
      document.querySelectorAll(".nav-item").forEach((item) => item.classList.remove("active"));
      document.querySelectorAll(".view").forEach((view) => view.classList.remove("active-view"));
      button.classList.add("active");
      el(button.dataset.view).classList.add("active-view");
      el("view-title").textContent = button.textContent;
    });
  });
}

function wireForms() {
  el("client-json").addEventListener("input", renderClientPreview);
  el("module-json").addEventListener("input", renderModulePreview);

  el("client-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
      await submitJsonForm("client-json", "client-connections");
      setMessage("Client connection registered.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("update-client-button").addEventListener("click", async () => {
    try {
      const payload = JSON.parse(el("client-json").value);
      if (!payload.id) {
        throw new Error("Client connection id is required.");
      }

      const result = await api(`client-connections/${encodeURIComponent(payload.id)}`, {
        method: "PUT",
        body: JSON.stringify(payload)
      });
      state.selectedClientId = result.id;
      await refreshAll();
      el("client-json").value = pretty(result);
      renderClientPreview();
      setMessage("Client connection updated.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("module-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
      await submitJsonForm("module-json", "modules");
      setMessage("Module registered.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("notification-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
      await submitJsonForm("notification-json", "notification-subscriptions");
      setMessage("Notification hook registered.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("job-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
      const result = await submitJsonForm("job-json", "jobs");
      el("job-id").value = result.id;
      renderJobResult(result);
      el("job-output").textContent = pretty(result);
      setMessage("Job submitted.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("data-consumer-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
      await submitJsonForm("data-consumer-json", "data-consumers");
      setMessage("Data consumer registered.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("import-module-button").addEventListener("click", async () => {
    try {
      await importModuleRelease(JSON.parse(el("module-import-json").value));
      await refreshAll();
      setMessage("Module release imported.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("compose-job-button").addEventListener("click", () => {
    try {
      const payload = composeJobRequestFromControls();
      el("job-json").value = pretty(payload);
      el("job-readiness").classList.add("hidden");
      el("job-provisioning-plan").classList.add("hidden");
      setMessage("Job request composed.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("check-job-readiness-button").addEventListener("click", async () => {
    try {
      const payload = JSON.parse(el("job-json").value);
      const result = await api("readiness/check", {
        method: "POST",
        body: JSON.stringify(readinessRequestFromJobPayload(payload))
      });
      renderReadinessResult(result);
      setMessage(result.isReady ? "Job readiness check passed." : "Job readiness check found blockers.", result.isReady ? "ok" : "warn");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("show-provisioning-plan-button").addEventListener("click", async () => {
    try {
      const payload = JSON.parse(el("job-json").value);
      const result = await api("provisioning/plan", {
        method: "POST",
        body: JSON.stringify(provisioningPlanRequestFromJobPayload(payload))
      });
      renderProvisioningPlan(result);
      setMessage(result.isExecutionReady ? "Provisioning plan is complete." : "Provisioning plan has required actions.", result.isExecutionReady ? "ok" : "warn");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("load-job-button").addEventListener("click", async () => {
    try {
      const job = await api(`jobs/${encodeURIComponent(el("job-id").value.trim())}`);
      renderJobResult(job);
      el("job-output").textContent = pretty(job);
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("collect-job-button").addEventListener("click", async () => {
    try {
      const job = await api(`jobs/${encodeURIComponent(el("job-id").value.trim())}/collect-result`, {
        method: "POST"
      });
      await refreshAll();
      renderJobResult(job);
      el("job-output").textContent = pretty(job);
      setMessage("Job result collected.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("process-artifact-button").addEventListener("click", async () => {
    try {
      const jobId = el("artifact-job-id").value.trim();
      const artifactName = el("artifact-name").value.trim() || "result";
      if (!jobId) {
        throw new Error("Job ID is required.");
      }

      const result = await api(`jobs/${encodeURIComponent(jobId)}/artifacts/${encodeURIComponent(artifactName)}/process`, {
        method: "POST",
        body: JSON.stringify(JSON.parse(el("artifact-process-json").value))
      });
      el("derived-artifact-id").value = result.id;
      el("derived-artifact-output").textContent = pretty(result);
      await loadDerivedArtifacts(jobId);
      setMessage("Artifact processed.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("list-derived-artifacts-button").addEventListener("click", async () => {
    try {
      const jobId = el("artifact-job-id").value.trim();
      if (!jobId) {
        throw new Error("Job ID is required.");
      }

      await loadDerivedArtifacts(jobId);
      setMessage("Derived artifacts loaded.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("load-derived-artifact-button").addEventListener("click", async () => {
    try {
      const jobId = el("artifact-job-id").value.trim();
      const artifactId = el("derived-artifact-id").value.trim();
      if (!jobId || !artifactId) {
        throw new Error("Job ID and derived artifact ID are required.");
      }

      const result = await api(`jobs/${encodeURIComponent(jobId)}/derived-artifacts/${encodeURIComponent(artifactId)}`);
      el("derived-artifact-output").textContent = pretty(result);
      setMessage("Derived artifact loaded.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("demo-import-module-button").addEventListener("click", async () => {
    try {
      await importModuleRelease(samples.accountReportImport);
      await refreshAll();
      setMessage("Account report module imported.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("demo-register-client-button").addEventListener("click", async () => {
    try {
      const payload = JSON.parse(el("demo-client-json").value);
      await registerOrUpdateClient(payload);
      await refreshAll();
      setMessage("Demo client saved.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("demo-submit-job-button").addEventListener("click", async () => {
    try {
      const payload = JSON.parse(el("demo-job-json").value);
      const job = await api("jobs", {
        method: "POST",
        body: JSON.stringify(payload)
      });
      el("demo-job-id").value = job.id;
      el("job-id").value = job.id;
      el("artifact-job-id").value = job.id;
      renderDemoResult(job);
      await refreshAll();
      setMessage("Demo job submitted.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });

  el("demo-collect-job-button").addEventListener("click", async () => {
    try {
      const jobId = el("demo-job-id").value.trim();
      if (!jobId) {
        throw new Error("Job ID is required.");
      }

      const job = await api(`jobs/${encodeURIComponent(jobId)}/collect-result`, {
        method: "POST"
      });
      renderDemoResult(job);
      el("job-id").value = job.id;
      el("artifact-job-id").value = job.id;
      el("job-output").textContent = pretty(job);
      renderJobResult(job);
      await refreshAll();
      setMessage("Demo result collected.");
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });
}

async function loadDerivedArtifacts(jobId) {
  const artifacts = await api(`jobs/${encodeURIComponent(jobId)}/derived-artifacts`);
  state.derivedArtifacts = artifacts;
  renderDerivedArtifacts(artifacts);
  return artifacts;
}

function wireSettings() {
  el("api-base-url").value = state.apiBaseUrl;
  el("settings-button").addEventListener("click", () => {
    el("settings-panel").classList.toggle("hidden");
  });
  el("save-settings-button").addEventListener("click", async () => {
    state.apiBaseUrl = el("api-base-url").value.trim();
    localStorage.setItem("apiBaseUrl", state.apiBaseUrl);
    setMessage("API base URL saved.");
    await refreshAll();
  });
  el("refresh-button").addEventListener("click", refreshAll);
}

function seedTextareas() {
  el("client-json").value = pretty(samples.client);
  el("module-import-json").value = pretty(samples.accountReportImport);
  el("module-json").value = pretty(samples.module);
  el("job-targets").value = samples.job.targetScope.targets.map((target) => target.userPrincipalName || target.id).join("\n");
  el("job-parameters").value = pretty(samples.job.parameters);
  el("job-json").value = pretty(samples.job);
  el("notification-json").value = pretty(samples.notification);
  el("demo-client-json").value = pretty(samples.accountReportClient);
  el("demo-job-json").value = pretty(samples.accountReportJob);
  el("data-consumer-json").value = pretty(samples.dataConsumer);
  el("artifact-process-json").value = pretty(samples.artifactProcessRequest);
  renderClientPreview();
  renderModulePreview();
}

wireNavigation();
wireSettings();
wireForms();
seedTextareas();
render();

async function start() {
  try {
    await initializeAuth();
    if (state.apiBaseUrl) {
      await refreshAll();
    } else {
      el("settings-panel").classList.remove("hidden");
      setHealth("Configure", "warn");
    }
  } catch (error) {
    setHealth("Attention", "warn");
    setMessage(error.message, "bad");
  }
}

start();
