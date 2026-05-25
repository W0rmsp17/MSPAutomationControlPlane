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
