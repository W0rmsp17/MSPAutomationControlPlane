using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class ReadinessService(
    IModuleRepository moduleRepository,
    IClientConnectionRepository clientConnectionRepository)
{
    public async Task<Result<ReadinessCheckResult>> CheckAsync(
        ReadinessCheckRequest request,
        CancellationToken cancellationToken)
    {
        var module = string.IsNullOrWhiteSpace(request.ModuleVersion)
            ? await moduleRepository.GetLatestAsync(request.ModuleId, cancellationToken)
            : await moduleRepository.GetAsync(request.ModuleId, request.ModuleVersion, cancellationToken);

        var clientConnection = await clientConnectionRepository.GetAsync(request.ClientConnectionId, cancellationToken);
        if (module is null && clientConnection is null)
        {
            return Result<ReadinessCheckResult>.Failure(
                $"Module '{request.ModuleId}' and client connection '{request.ClientConnectionId}' were not found.");
        }

        if (module is null)
        {
            return Result<ReadinessCheckResult>.Failure($"Module '{request.ModuleId}' was not found.");
        }

        if (clientConnection is null)
        {
            return Result<ReadinessCheckResult>.Failure($"Client connection '{request.ClientConnectionId}' was not found.");
        }

        var blockingIssues = new List<string>();
        var warnings = new List<string>();

        if (!clientConnection.Enabled)
        {
            blockingIssues.Add($"Client connection '{clientConnection.Id}' is disabled.");
        }

        if (clientConnection.ReadinessStatus != ClientConnectionReadinessStatus.Ready)
        {
            blockingIssues.Add($"Client connection '{clientConnection.Id}' readiness is '{clientConnection.ReadinessStatus}'.");
        }

        if (clientConnection.EnabledModuleIds.Count > 0 &&
            !clientConnection.EnabledModuleIds.Contains(module.Manifest.Id, StringComparer.OrdinalIgnoreCase))
        {
            blockingIssues.Add($"Module '{module.Manifest.Id}' is not enabled for client connection '{clientConnection.Id}'.");
        }

        if (request.TargetScopeType is not null)
        {
            if (!module.Manifest.SupportedScopes.Contains(request.TargetScopeType.Value))
            {
                blockingIssues.Add($"Module '{module.Manifest.Id}' does not support target scope '{request.TargetScopeType}'.");
            }

            if (!clientConnection.AllowedScopes.Contains(request.TargetScopeType.Value))
            {
                blockingIssues.Add($"Client connection '{clientConnection.Id}' does not allow target scope '{request.TargetScopeType}'.");
            }
        }
        else
        {
            warnings.Add("Target scope was not supplied, so scope compatibility was not checked.");
        }

        var matchingPermissions = new List<ConfiguredPermission>();
        foreach (var requiredPermission in module.Manifest.RequiredPermissions)
        {
            var configuredPermission = clientConnection.ConfiguredPermissions.FirstOrDefault(permission =>
                string.Equals(permission.Provider, requiredPermission.Provider, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(permission.Permission, requiredPermission.Permission, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(permission.Type, requiredPermission.Type, StringComparison.OrdinalIgnoreCase));

            if (configuredPermission is null)
            {
                blockingIssues.Add(
                    $"Missing permission '{requiredPermission.Provider}/{requiredPermission.Permission}' ({requiredPermission.Type}).");
                continue;
            }

            matchingPermissions.Add(configuredPermission);
            if (!configuredPermission.AdminConsented)
            {
                blockingIssues.Add(
                    $"Permission '{requiredPermission.Provider}/{requiredPermission.Permission}' ({requiredPermission.Type}) has not been admin-consented.");
            }
        }

        if (clientConnection.ConfiguredPermissions.Count == 0 && module.Manifest.RequiredPermissions.Count == 0)
        {
            warnings.Add("Module declares no required permissions.");
        }

        return Result<ReadinessCheckResult>.Success(new ReadinessCheckResult
        {
            ClientConnectionId = clientConnection.Id,
            ModuleId = module.Manifest.Id,
            ModuleVersion = module.Manifest.Version,
            TargetScopeType = request.TargetScopeType,
            BlockingIssues = blockingIssues,
            Warnings = warnings,
            RequiredPermissions = module.Manifest.RequiredPermissions,
            MatchingPermissions = matchingPermissions
        });
    }
}
