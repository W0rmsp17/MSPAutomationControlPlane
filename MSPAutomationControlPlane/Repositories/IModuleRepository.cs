using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public interface IModuleRepository
{
    Task AddAsync(ModuleRegistration registration, CancellationToken cancellationToken);

    Task<ModuleRegistration?> GetAsync(string moduleId, string version, CancellationToken cancellationToken);

    Task<ModuleRegistration?> GetLatestAsync(string moduleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ModuleRegistration>> ListAsync(CancellationToken cancellationToken);
}
