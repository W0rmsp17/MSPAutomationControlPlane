const config = window.MSP_CONTROL_PLANE_CONFIG || {};
const state = {
  apiBaseUrl: localStorage.getItem("apiBaseUrl") || config.apiBaseUrl || "",
  auth: config.auth || {},
  msalClient: null,
  msalAccount: null,
  clients: [],
  modules: [],
  jobs: [],
  notifications: [],
  auditEvents: []
};

const samples = {
  client: {
    id: "client-plutonix",
    displayName: "Plutonix",
    tenantId: "00000000-0000-0000-0000-000000000000",
    executionMode: "Central",
    executionAppClientId: "00000000-0000-0000-0000-000000000000",
    certificateReference: "kv://clients/client-plutonix/graph-certificate",
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
    version: "0.1.0",
    description: "Validates the control plane module registration and job contract.",
    image: "ghcr.io/example/tenant-health-check:0.1.0",
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

const el = (id) => document.getElementById(id);

function pretty(value) {
  return JSON.stringify(value, null, 2);
}

function endpoint(path) {
  return `${state.apiBaseUrl.replace(/\/$/, "")}/${path.replace(/^\//, "")}`;
}

function authConfigured() {
  return Boolean(state.auth.tenantId && state.auth.clientId && state.auth.apiScope && window.msal);
}

async function initializeAuth() {
  if (!authConfigured()) {
    return;
  }

  state.msalClient = new msal.PublicClientApplication({
    auth: {
      clientId: state.auth.clientId,
      authority: `https://login.microsoftonline.com/${state.auth.tenantId}`,
      redirectUri: window.location.origin
    },
    cache: {
      cacheLocation: "sessionStorage"
    }
  });

  const redirectResult = await state.msalClient.handleRedirectPromise();
  state.msalAccount = redirectResult?.account || state.msalClient.getAllAccounts()[0] || null;
  if (!state.msalAccount) {
    await state.msalClient.loginRedirect({
      scopes: [state.auth.apiScope]
    });
  }
}

async function acquireAccessToken() {
  if (!authConfigured()) {
    return null;
  }

  if (!state.msalAccount) {
    await initializeAuth();
  }

  try {
    const result = await state.msalClient.acquireTokenSilent({
      account: state.msalAccount,
      scopes: [state.auth.apiScope]
    });
    return result.accessToken;
  } catch (error) {
    if (error instanceof msal.InteractionRequiredAuthError) {
      await state.msalClient.acquireTokenRedirect({
        account: state.msalAccount,
        scopes: [state.auth.apiScope]
      });
      return null;
    }

    throw error;
  }
}

async function api(path, options = {}) {
  if (!state.apiBaseUrl) {
    throw new Error("Set the API base URL before calling the control plane.");
  }

  const accessToken = await acquireAccessToken();
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
  target.innerHTML = "";

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
      message.textContent = finding.message || "";

      item.appendChild(findingTitle);
      item.appendChild(findingMeta);
      item.appendChild(message);
      findings.appendChild(item);
    });
    target.appendChild(findings);
  }

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

function render() {
  el("metric-clients").textContent = state.clients.length;
  el("metric-modules").textContent = state.modules.length;
  el("metric-notifications").textContent = state.notifications.length;
  el("metric-audit").textContent = state.jobs.length;

  renderList("clients-list", state.clients, (client) =>
    listItem(
      client.displayName || client.id,
      `${client.tenantId || ""} - ${client.executionMode || ""} - ${client.readinessStatus || "Unknown"}`,
      client.enabled ? "Enabled" : "Disabled"));

  renderList("modules-list", state.modules, (module) => {
    const manifest = module.manifest || module;
    return listItem(manifest.name || manifest.id, `${manifest.id || ""} - ${manifest.version || ""}`, manifest.runtime || "module");
  });

  renderList("notifications-list", state.notifications, (subscription) =>
    listItem(subscription.displayName || subscription.id, subscription.targetUrl || "", subscription.enabled ? "Enabled" : "Disabled"));

  renderList("jobs-list", state.jobs, (job) => {
    const item = listItem(
      job.id,
      `${job.moduleId || ""} - ${job.tenantContext?.tenantName || job.tenantContext?.clientId || ""} - ${job.createdAt || ""}`,
      job.status || "Unknown");

    item.addEventListener("click", () => {
      el("job-id").value = job.id;
      renderJobResult(job);
      el("job-output").textContent = pretty(job);
    });
    return item;
  });

  renderTimeline("audit-list", state.auditEvents);
  renderTimeline("recent-activity", state.auditEvents);
}

async function refreshAll() {
  try {
    setHealth("Checking", "muted");
    await api("health");
    setHealth("Healthy", "ok");

    const [clients, modules, jobs, notifications, auditEvents] = await Promise.all([
      api("client-connections"),
      api("modules"),
      api("jobs"),
      api("notification-subscriptions"),
      api("audit-events")
    ]);

    state.clients = clients;
    state.modules = modules;
    state.jobs = jobs;
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
  el("client-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
      await submitJsonForm("client-json", "client-connections");
      setMessage("Client connection registered.");
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

  el("load-job-button").addEventListener("click", async () => {
    try {
      const job = await api(`jobs/${encodeURIComponent(el("job-id").value.trim())}`);
      renderJobResult(job);
      el("job-output").textContent = pretty(job);
    } catch (error) {
      setMessage(error.message, "bad");
    }
  });
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
  el("module-json").value = pretty(samples.module);
  el("job-json").value = pretty(samples.job);
  el("notification-json").value = pretty(samples.notification);
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
