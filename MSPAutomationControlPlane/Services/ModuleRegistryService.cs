using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class ModuleRegistryService(
    IModuleRepository moduleRepository,
    IOperatorContext operatorContext)
{
    private static readonly HashSet<string> SupportedRuntimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "container-apps-job"
    };

    public async Task<Result<ModuleRegistration>> RegisterAsync(
        ModuleManifest manifest,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(manifest);
        if (validationError is not null)
        {
            return Result<ModuleRegistration>.Failure(validationError);
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
        return Result<ModuleRegistration>.Success(registration);
    }

    public Task<IReadOnlyList<ModuleRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        return moduleRepository.ListAsync(cancellationToken);
    }

    private static string? Validate(ModuleManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            return "Module id is required.";
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            return "Module name is required.";
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            return "Module version is required.";
        }

        if (string.IsNullOrWhiteSpace(manifest.Image))
        {
            return "Module image is required.";
        }

        if (!SupportedRuntimes.Contains(manifest.Runtime))
        {
            return $"Runtime '{manifest.Runtime}' is not supported.";
        }

        if (manifest.SupportedScopes.Count == 0)
        {
            return "At least one supported target scope is required.";
        }

        if (manifest.TimeoutSeconds <= 0)
        {
            return "TimeoutSeconds must be greater than zero.";
        }

        if (manifest.Concurrency <= 0)
        {
            return "Concurrency must be greater than zero.";
        }

        return null;
    }
}
