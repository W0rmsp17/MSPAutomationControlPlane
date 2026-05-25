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

resource "random_string" "suffix" {
  length  = 6
  lower   = true
  numeric = true
  special = false
  upper   = false
}

locals {
  normalized_prefix = lower(replace(var.name_prefix, "-", ""))
  suffix            = random_string.suffix.result
  resource_suffix   = "${var.environment_name}-${local.suffix}"

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
    FUNCTIONS_WORKER_RUNTIME                 = "dotnet-isolated"
    WEBSITE_RUN_FROM_PACKAGE                 = "1"
    ControlPlane__RepositoryProvider         = "TableStorage"
    ControlPlane__StorageConnectionString    = azurerm_storage_account.main.primary_connection_string
    ControlPlane__TablePrefix                = var.table_prefix
    ControlPlane__QueueProvider              = "ServiceBus"
    ControlPlane__ServiceBusConnectionString = azurerm_servicebus_queue_authorization_rule.jobs_send_listen.primary_connection_string
    ControlPlane__JobQueueName               = azurerm_servicebus_queue.jobs.name
    ServiceBusConnection                     = azurerm_servicebus_queue_authorization_rule.jobs_send_listen.primary_connection_string
    ServiceBusJobQueueName                   = azurerm_servicebus_queue.jobs.name
    Artifacts__ContainerName                 = azurerm_storage_container.artifacts.name
    KeyVault__Uri                            = azurerm_key_vault.main.vault_uri
  }
}

resource "azurerm_static_web_app" "frontend" {
  name                = "swa-${var.name_prefix}-${local.resource_suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.static_web_app_location
  sku_tier            = var.static_web_app_sku_tier
  sku_size            = var.static_web_app_sku_size
  tags                = local.tags
}

resource "azurerm_container_app_environment" "workers" {
  name                       = "cae-${var.name_prefix}-${local.resource_suffix}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags
}
