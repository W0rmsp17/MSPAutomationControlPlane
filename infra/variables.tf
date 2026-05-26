variable "subscription_id" {
  description = "Azure subscription ID that hosts the central MSP control plane."
  type        = string
}

variable "msp_tenant_id" {
  description = "Microsoft Entra tenant ID for the MSP tenant."
  type        = string
}

variable "name_prefix" {
  description = "Short lowercase prefix used in resource names."
  type        = string
  default     = "mspcp"
}

variable "environment_name" {
  description = "Environment name, such as dev, test, or prod."
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region for regional resources."
  type        = string
  default     = "australiaeast"
}

variable "static_web_app_location" {
  description = "Static Web App location."
  type        = string
  default     = "eastasia"
}

variable "storage_replication_type" {
  description = "Storage account replication type."
  type        = string
  default     = "LRS"
}

variable "service_bus_sku" {
  description = "Service Bus namespace SKU."
  type        = string
  default     = "Basic"
}

variable "job_queue_name" {
  description = "Service Bus queue name for job dispatch."
  type        = string
  default     = "jobs"
}

variable "function_plan_sku" {
  description = "Function App service plan SKU. Y1 is consumption."
  type        = string
  default     = "Y1"
}

variable "function_cors_allowed_origins" {
  description = "Additional origins allowed to call the Function App API from browsers."
  type        = list(string)
  default     = []
}

variable "table_prefix" {
  description = "Azure Table Storage table name prefix."
  type        = string
  default     = "MspControl"
}

variable "allowed_module_registries" {
  description = "Container registry hostnames allowed for module manifest image references."
  type        = list(string)
  default     = ["ghcr.io", "mcr.microsoft.com"]
}

variable "execution_provider" {
  description = "Module execution provider. Use LocalOrSimulated for labs until Container Apps result collection is configured."
  type        = string
  default     = "LocalOrSimulated"
}

variable "container_job_placeholder_image" {
  description = "Placeholder image used by the reusable Container Apps Job definition. Real executions override this from the registered module manifest."
  type        = string
  default     = "mcr.microsoft.com/k8se/quickstart-jobs:latest"
}

variable "container_job_cpu" {
  description = "Default CPU cores for module worker executions."
  type        = number
  default     = 0.25
}

variable "container_job_memory" {
  description = "Default memory for module worker executions."
  type        = string
  default     = "0.5Gi"
}

variable "container_job_replica_timeout_seconds" {
  description = "Maximum runtime in seconds for a Container Apps module worker replica."
  type        = number
  default     = 900
}

variable "container_job_replica_retry_limit" {
  description = "Retry limit for Container Apps module worker replicas."
  type        = number
  default     = 0
}

variable "container_registry_server" {
  description = "Optional private container registry server for module images, such as ghcr.io."
  type        = string
  default     = ""
}

variable "container_registry_username" {
  description = "Optional private container registry username."
  type        = string
  default     = ""
}

variable "container_registry_password" {
  description = "Optional private container registry password or token."
  type        = string
  default     = ""
  sensitive   = true
}

variable "log_retention_days" {
  description = "Log Analytics retention in days."
  type        = number
  default     = 30
}

variable "enable_key_vault_purge_protection" {
  description = "Enable purge protection for Key Vault. Keep false for lab teardown."
  type        = bool
  default     = false
}

variable "static_web_app_sku_tier" {
  description = "Static Web App SKU tier."
  type        = string
  default     = "Free"
}

variable "static_web_app_sku_size" {
  description = "Static Web App SKU size."
  type        = string
  default     = "Free"
}

variable "tags" {
  description = "Additional tags."
  type        = map(string)
  default     = {}
}
