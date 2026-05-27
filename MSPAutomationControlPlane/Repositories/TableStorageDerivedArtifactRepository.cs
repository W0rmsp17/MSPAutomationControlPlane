using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class TableStorageDerivedArtifactRepository(TableStorageOptions options) : IDerivedArtifactRepository
{
    private readonly TableJsonStore<DerivedArtifact> _store = new(options, "DerivedArtifacts");

    public Task AddAsync(DerivedArtifact artifact, CancellationToken cancellationToken)
    {
        return _store.UpsertAsync(artifact.JobId, artifact.Id, artifact, cancellationToken);
    }

    public Task<DerivedArtifact?> GetAsync(string jobId, string artifactId, CancellationToken cancellationToken)
    {
        return _store.GetAsync(jobId, artifactId, cancellationToken);
    }

    public async Task<IReadOnlyList<DerivedArtifact>> ListByJobAsync(string jobId, CancellationToken cancellationToken)
    {
        var artifacts = await _store.ListPartitionAsync(jobId, cancellationToken);
        return artifacts
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();
    }
}
