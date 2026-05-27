using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class ProvisioningPlanService(
    IModuleRepository moduleRepository,
    IClientConnectionRepository clientConnectionRepository,
    ReadinessService readinessService)
{
    public async Task<Result<ProvisioningPlan>> CreateAsync(
        ProvisioningPlanRequest request,
        CancellationToken cancellationToken)
    {
        var module = string.IsNullOrWhiteSpace(request.ModuleVersion)
            ? await moduleRepository.GetLatestAsync(request.ModuleId, cancellationToken)
            : await moduleRepository.GetAsync(request.ModuleId, request.ModuleVersion, cancellationToken);

        var clientConnection = await clientConnectionRepository.GetAsync(request.ClientConnectionId, cancellationToken);
        if (module is null && clientConnection is null)
        {
            return Result<ProvisioningPlan>.Failure(
                $"Module '{request.ModuleId}' and client connection '{request.ClientConnectionId}' were not found.");
        }

        if (module is null)
        {
            return Result<ProvisioningPlan>.Failure($"Module '{request.ModuleId}' was not found.");
        }

        if (clientConnection is null)
        {
            return Result<ProvisioningPlan>.Failure($"Client connection '{request.ClientConnectionId}' was not found.");
        }

        var readiness = await readinessService.CheckAsync(
            new ReadinessCheckRequest
            {
                ClientConnectionId = clientConnection.Id,
                ModuleId = module.Manifest.Id,
                ModuleVersion = module.Manifest.Version
            },
            cancellationToken);

        var blockingIssues = readiness.Succeeded
            ? readiness.Value!.BlockingIssues
            : readiness.Errors;

        return Result<ProvisioningPlan>.Success(new ProvisioningPlan
        {
            ClientConnectionId = clientConnection.Id,
            ClientDisplayName = clientConnection.DisplayName,
            TenantId = clientConnection.TenantId,
            ModuleId = module.Manifest.Id,
            ModuleVersion = module.Manifest.Version,
            IsExecutionReady = readiness.Succeeded && readiness.Value!.IsReady,
            BlockingIssues = blockingIssues,
            RequiredPermissions = module.Manifest.RequiredPermissions,
            RecommendedCertificateReference = $"kv://certificates/{clientConnection.Id}-graph",
            Steps = BuildSteps(clientConnection, module.Manifest, blockingIssues)
        });
    }

    private static IReadOnlyList<ProvisioningPlanStep> BuildSteps(
        ClientConnection clientConnection,
        ModuleManifest manifest,
        IReadOnlyList<string> blockingIssues)
    {
        var order = 1;
        var steps = new List<ProvisioningPlanStep>
        {
            new()
            {
                Order = order++,
                Status = string.IsNullOrWhiteSpace(clientConnection.ExecutionAppClientId)
                    ? ProvisioningPlanStepStatus.Required
                    : ProvisioningPlanStepStatus.Complete,
                Title = "Create or select target tenant execution app",
                Detail = "Create an app registration in the client tenant, or provide the existing app/client ID for this client connection.",
                Owner = "Target tenant administrator"
            },
            new()
            {
                Order = order++,
                Status = IsRuntimeCertificateReference(clientConnection.CertificateReference)
                    ? ProvisioningPlanStepStatus.Complete
                    : ProvisioningPlanStepStatus.Required,
                Title = "Provision execution certificate",
                Detail = $"Add a certificate credential to the target tenant app and store the certificate with private key in MSP Key Vault. Recommended reference: kv://certificates/{clientConnection.Id}-graph.",
                Owner = "MSP platform administrator"
            }
        };

        foreach (var permission in manifest.RequiredPermissions)
        {
            var configuredPermission = clientConnection.ConfiguredPermissions.FirstOrDefault(configured =>
                string.Equals(configured.Provider, permission.Provider, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(configured.Permission, permission.Permission, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(configured.Type, permission.Type, StringComparison.OrdinalIgnoreCase));

            steps.Add(new ProvisioningPlanStep
            {
                Order = order++,
                Status = configuredPermission?.AdminConsented == true
                    ? ProvisioningPlanStepStatus.Complete
                    : ProvisioningPlanStepStatus.Required,
                Title = $"Grant {permission.Provider}/{permission.Permission}",
                Detail = permission.Reason ??
                    $"Grant and admin-consent {permission.Type} permission '{permission.Permission}' for provider '{permission.Provider}'.",
                Owner = "Target tenant administrator"
            });
        }

        steps.Add(new ProvisioningPlanStep
        {
            Order = order++,
            Status = clientConnection.EnabledModuleIds.Count == 0 ||
                clientConnection.EnabledModuleIds.Contains(manifest.Id, StringComparer.OrdinalIgnoreCase)
                    ? ProvisioningPlanStepStatus.Complete
                    : ProvisioningPlanStepStatus.Required,
            Title = "Enable module for client connection",
            Detail = $"Allow module '{manifest.Id}' for client connection '{clientConnection.Id}'.",
            Owner = "MSP operator"
        });

        steps.Add(new ProvisioningPlanStep
        {
            Order = order++,
            Status = blockingIssues.Count == 0
                ? ProvisioningPlanStepStatus.Complete
                : ProvisioningPlanStepStatus.Blocked,
            Title = "Run readiness check",
            Detail = blockingIssues.Count == 0
                ? "Readiness check passes. The module/client pair can be submitted for execution."
                : "Resolve the blocking issues returned by the readiness check before submitting jobs.",
            Owner = "MSP operator"
        });

        return steps;
    }

    private static bool IsRuntimeCertificateReference(string? certificateReference)
    {
        return !string.IsNullOrWhiteSpace(certificateReference) &&
            CertificateReferenceResolver.TryResolveCertificateName(certificateReference, out _, out _);
    }
}
