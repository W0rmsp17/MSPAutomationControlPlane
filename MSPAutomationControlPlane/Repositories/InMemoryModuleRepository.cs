using System.Collections.Concurrent;
using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class InMemoryModuleRepository : IModuleRepository
{
    private readonly ConcurrentDictionary<string, ModuleRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(ModuleRegistration registration, CancellationToken cancellationToken)
    {
        _registrations[GetKey(registration.Manifest.Id, registration.Manifest.Version)] = registration;
        return Task.CompletedTask;
    }

    public Task<ModuleRegistration?> GetAsync(string moduleId, string version, CancellationToken cancellationToken)
    {
        _registrations.TryGetValue(GetKey(moduleId, version), out var registration);
        return Task.FromResult(registration);
    }

    public Task<ModuleRegistration?> GetLatestAsync(string moduleId, CancellationToken cancellationToken)
    {
        var registration = _registrations.Values
            .Where(item => string.Equals(item.Manifest.Id, moduleId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.RegisteredAt)
            .FirstOrDefault();

        return Task.FromResult(registration);
    }

    public Task<IReadOnlyList<ModuleRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        var registrations = _registrations.Values
            .OrderBy(item => item.Manifest.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Manifest.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ModuleRegistration>>(registrations);
    }

    private static string GetKey(string moduleId, string version) => $"{moduleId}:{version}";
}
