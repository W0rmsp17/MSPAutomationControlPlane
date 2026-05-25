using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class ModuleRegistryService(
    IModuleRepository moduleRepository,
    ModuleManifestValidator manifestValidator,
    AuditService auditService,
    IOperatorContext operatorContext)
{
    public async Task<Result<ModuleRegistration>> RegisterAsync(
        ModuleManifest manifest,
        CancellationToken cancellationToken)
    {
        var validation = manifestValidator.Validate(manifest);
        if (!validation.Succeeded)
        {
            return Result<ModuleRegistration>.Failure(validation.Errors);
        }

        var existing = await moduleRepository.GetAsync(manifest.Id, manifest.Version, cancellationToken);
        if (existing is not null)
        {
            return Result<ModuleRegistration>.Failure(
                $"Module '{manifest.Id}' version '{manifest.Version}' is already registered.");
        }

        var registration = new ModuleRegistration
        {
            Manifest = manifest,
            RegisteredBy = operatorContext.CurrentOperator,
            RegisteredAt = DateTimeOffset.UtcNow
        };

        await moduleRepository.AddAsync(registration, cancellationToken);
        await auditService.WriteAsync(
            AuditEventType.ModuleRegistered,
            operatorContext.CurrentOperator,
            $"Module '{manifest.Id}' version '{manifest.Version}' was registered.",
            cancellationToken,
            moduleId: manifest.Id,
            resourceId: $"{manifest.Id}:{manifest.Version}");

        return Result<ModuleRegistration>.Success(registration);
    }

    public Task<IReadOnlyList<ModuleRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        return moduleRepository.ListAsync(cancellationToken);
    }
}
