terraform {
  required_version = ">= 1.7.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.116"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
  tenant_id       = var.msp_tenant_id
}

provider "random" {}

resource "random_string" "suffix" {
  length  = 6
  lower   = true
  numeric = true
  special = false
  upper   = false
}

resource "random_password" "runtime_broker_signing_key" {
  length  = 48
  special = false
}

locals {
  normalized_prefix = lower(replace(var.name_prefix, "-", ""))
  suffix            = random_string.suffix.result
  resource_suffix   = "${var.environment_name}-${local.suffix}"
  use_private_registry = (
    var.container_registry_server != "" &&
    var.container_registry_username != "" &&
    var.container_registry_password != ""
  )

  tags = merge(
    {
      workload    = "msp-automation-control-plane"
      environment = var.environment_name
      managed_by  = "terraform"
    },
    var.tags
  )
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.name_prefix}-${var.environment_name}"
  location = var.location
  tags     = local.tags
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${var.name_prefix}-${local.resource_suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = var.log_retention_days
  tags                = local.tags
}

resource "azurerm_application_insights" "main" {
  name                = "appi-${var.name_prefix}-${local.resource_suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.tags
}

resource "azurerm_storage_account" "main" {
  name                            = substr("st${local.normalized_prefix}${var.environment_name}${local.suffix}", 0, 24)
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = var.storage_replication_type
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  tags                            = local.tags
}

resource "azurerm_storage_container" "artifacts" {
  name                  = "artifacts"
  storage_account_name  = azurerm_storage_account.main.name
  container_access_type = "private"
}

resource "azurerm_key_vault" "main" {
  name                       = substr("kv-${var.name_prefix}-${local.resource_suffix}", 0, 24)
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = var.msp_tenant_id
  sku_name                   = "standard"
  enable_rbac_authorization  = true
  purge_protection_enabled   = var.enable_key_vault_purge_protection
  soft_delete_retention_days = 7
  tags                       = local.tags
}

resource "azurerm_servicebus_namespace" "main" {
  name                = "sb-${var.name_prefix}-${local.resource_suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = var.service_bus_sku
  tags                = local.tags
}

resource "azurerm_servicebus_queue" "jobs" {
  name         = var.job_queue_name
  namespace_id = azurerm_servicebus_namespace.main.id

  dead_lettering_on_message_expiration = true
  default_message_ttl                  = "P14D"
  lock_duration                        = "PT1M"
  max_delivery_count                   = 5
}

resource "azurerm_servicebus_queue_authorization_rule" "jobs_send_listen" {
  name     = "control-plane-jobs-send-listen"
  queue_id = azurerm_servicebus_queue.jobs.id

  listen = true
  send   = true
  manage = false
}

resource "azurerm_service_plan" "function" {
  name                = "asp-${var.name_prefix}-${local.resource_suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  os_type             = "Windows"
  sku_name            = var.function_plan_sku
  tags                = local.tags
}

resource "azurerm_windows_function_app" "control_api" {
  name                = "func-${var.name_prefix}-${local.resource_suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  service_plan_id     = azurerm_service_plan.function.id

  storage_account_name       = azurerm_storage_account.main.name
  storage_account_access_key = azurerm_storage_account.main.primary_access_key

  https_only = true
  tags       = local.tags

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version              = "v8.0"
      use_dotnet_isolated_runtime = true
    }

    application_insights_connection_string = azurerm_application_insights.main.connection_string
    ftps_state                             = "Disabled"
    minimum_tls_version                    = "1.2"

    cors {
      allowed_origins = concat(
        [
          "https://${azurerm_static_web_app.frontend.default_host_name}"
        ],
        var.function_cors_allowed_origins
      )
    }
  }

  app_settings = {
    FUNCTIONS_WORKER_RUNTIME                       = "dotnet-isolated"
    WEBSITE_RUN_FROM_PACKAGE                       = "1"
    ControlPlane__RepositoryProvider               = "TableStorage"
    ControlPlane__StorageConnectionString          = azurerm_storage_account.main.primary_connection_string
    ControlPlane__TablePrefix                      = var.table_prefix
    ControlPlane__Modules__AllowedRegistries       = join(",", var.allowed_module_registries)
    ControlPlane__ExecutionProvider                = var.execution_provider
    ControlPlane__RuntimeBroker__SigningKey        = random_password.runtime_broker_signing_key.result
    ControlPlane__RuntimeBroker__BaseUrl           = "https://func-${var.name_prefix}-${local.resource_suffix}.azurewebsites.net/api"
    ControlPlane__ContainerApps__SubscriptionId    = var.subscription_id
    ControlPlane__ContainerApps__ResourceGroupName = azurerm_resource_group.main.name
    ControlPlane__ContainerApps__JobName           = azurerm_container_app_job.module_worker.name
    ControlPlane__ContainerApps__ContainerName     = "module-worker"
    ControlPlane__ContainerApps__Cpu               = tostring(var.container_job_cpu)
    ControlPlane__ContainerApps__Memory            = var.container_job_memory
    ControlPlane__QueueProvider                    = "ServiceBus"
    ControlPlane__ServiceBusConnectionString       = azurerm_servicebus_queue_authorization_rule.jobs_send_listen.primary_connection_string
    ControlPlane__JobQueueName                     = azurerm_servicebus_queue.jobs.name
    ServiceBusConnection                           = azurerm_servicebus_queue_authorization_rule.jobs_send_listen.primary_connection_string
    ServiceBusJobQueueName                         = azurerm_servicebus_queue.jobs.name
    Artifacts__ContainerName                       = azurerm_storage_container.artifacts.name
    Artifacts__BlobServiceUri                      = azurerm_storage_account.main.primary_blob_endpoint
    KeyVault__Uri                                  = azurerm_key_vault.main.vault_uri
  }

  lifecycle {
    ignore_changes = [
      app_settings["ControlPlane__Auth__Enabled"],
      app_settings["ControlPlane__Auth__TenantId"],
      app_settings["ControlPlane__Auth__Audience"],
      app_settings["ControlPlane__Auth__RequiredScope"],
      app_settings["ControlPlane__Auth__AllowedUserObjectIds"],
      app_settings["ControlPlane__Auth__AllowedGroupIds"],
      app_settings["ControlPlane__Auth__AllowedRoles"]
    ]
  }
}

resource "azurerm_static_web_app" "frontend" {
  name                = "swa-${var.name_prefix}-${local.resource_suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.static_web_app_location
  sku_tier            = var.static_web_app_sku_tier
  sku_size            = var.static_web_app_sku_size
  tags                = local.tags

  lifecycle {
    ignore_changes = [
      app_settings
    ]
  }
}

resource "azurerm_container_app_environment" "workers" {
  name                       = "cae-${var.name_prefix}-${local.resource_suffix}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags
}

resource "azurerm_container_app_job" "module_worker" {
  name                         = "caj-${var.name_prefix}-${local.resource_suffix}"
  location                     = azurerm_resource_group.main.location
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.workers.id
  replica_timeout_in_seconds   = var.container_job_replica_timeout_seconds
  replica_retry_limit          = var.container_job_replica_retry_limit
  tags                         = local.tags

  identity {
    type = "SystemAssigned"
  }

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  dynamic "secret" {
    for_each = local.use_private_registry ? [1] : []

    content {
      name  = "module-registry-password"
      value = var.container_registry_password
    }
  }

  dynamic "registry" {
    for_each = local.use_private_registry ? [1] : []

    content {
      server               = var.container_registry_server
      username             = var.container_registry_username
      password_secret_name = "module-registry-password"
    }
  }

  template {
    container {
      name   = "module-worker"
      image  = var.container_job_placeholder_image
      cpu    = var.container_job_cpu
      memory = var.container_job_memory
    }
  }
}

resource "azurerm_role_assignment" "function_start_container_jobs" {
  scope                = azurerm_container_app_job.module_worker.id
  role_definition_name = "Contributor"
  principal_id         = azurerm_windows_function_app.control_api.identity[0].principal_id
}

resource "azurerm_role_assignment" "function_read_key_vault_certificates" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Certificate User"
  principal_id         = azurerm_windows_function_app.control_api.identity[0].principal_id
}

resource "azurerm_role_assignment" "function_read_key_vault_secrets" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_windows_function_app.control_api.identity[0].principal_id
}

resource "azurerm_role_assignment" "module_worker_write_artifacts" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_container_app_job.module_worker.identity[0].principal_id
}
