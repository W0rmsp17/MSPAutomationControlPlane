using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public interface IDerivedArtifactRepository
{
    Task AddAsync(DerivedArtifact artifact, CancellationToken cancellationToken);

    Task<DerivedArtifact?> GetAsync(string jobId, string artifactId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DerivedArtifact>> ListByJobAsync(string jobId, CancellationToken cancellationToken);
}
