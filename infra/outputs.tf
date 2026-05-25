output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "msp_tenant_id" {
  value = var.msp_tenant_id
}

output "function_app_name" {
  value = azurerm_windows_function_app.control_api.name
}

output "function_app_default_hostname" {
  value = azurerm_windows_function_app.control_api.default_hostname
}

output "static_web_app_name" {
  value = azurerm_static_web_app.frontend.name
}

output "static_web_app_default_host_name" {
  value = azurerm_static_web_app.frontend.default_host_name
}

output "storage_account_name" {
  value = azurerm_storage_account.main.name
}

output "service_bus_namespace_name" {
  value = azurerm_servicebus_namespace.main.name
}

output "job_queue_name" {
  value = azurerm_servicebus_queue.jobs.name
}

output "key_vault_name" {
  value = azurerm_key_vault.main.name
}

output "key_vault_uri" {
  value = azurerm_key_vault.main.vault_uri
}

output "container_app_environment_name" {
  value = azurerm_container_app_environment.workers.name
}
