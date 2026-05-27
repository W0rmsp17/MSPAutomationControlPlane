using System.Collections.Concurrent;
using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class InMemoryDerivedArtifactRepository : IDerivedArtifactRepository
{
    private readonly ConcurrentDictionary<string, DerivedArtifact> _artifacts = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(DerivedArtifact artifact, CancellationToken cancellationToken)
    {
        _artifacts[GetKey(artifact.JobId, artifact.Id)] = artifact;
        return Task.CompletedTask;
    }

    public Task<DerivedArtifact?> GetAsync(string jobId, string artifactId, CancellationToken cancellationToken)
    {
        _artifacts.TryGetValue(GetKey(jobId, artifactId), out var artifact);
        return Task.FromResult(artifact);
    }

    public Task<IReadOnlyList<DerivedArtifact>> ListByJobAsync(string jobId, CancellationToken cancellationToken)
    {
        var artifacts = _artifacts.Values
            .Where(item => string.Equals(item.JobId, jobId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyList<DerivedArtifact>>(artifacts);
    }

    private static string GetKey(string jobId, string artifactId) => $"{jobId}:{artifactId}";
}
