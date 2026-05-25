using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class TableStorageModuleRepository(TableStorageOptions options) : IModuleRepository
{
    private const string PartitionKey = "MODULE";
    private readonly TableJsonStore<ModuleRegistration> _store = new(options, "Modules");

    public Task AddAsync(ModuleRegistration registration, CancellationToken cancellationToken)
    {
        return _store.UpsertAsync(
            PartitionKey,
            GetKey(registration.Manifest.Id, registration.Manifest.Version),
            registration,
            cancellationToken);
    }

    public Task<ModuleRegistration?> GetAsync(string moduleId, string version, CancellationToken cancellationToken)
    {
        return _store.GetAsync(PartitionKey, GetKey(moduleId, version), cancellationToken);
    }

    public async Task<ModuleRegistration?> GetLatestAsync(string moduleId, CancellationToken cancellationToken)
    {
        var registrations = await ListAsync(cancellationToken);
        return registrations
            .Where(item => string.Equals(item.Manifest.Id, moduleId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.RegisteredAt)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<ModuleRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        var registrations = await _store.ListPartitionAsync(PartitionKey, cancellationToken);
        return registrations
            .OrderBy(item => item.Manifest.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Manifest.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetKey(string moduleId, string version) => $"{moduleId}:{version}";
}
