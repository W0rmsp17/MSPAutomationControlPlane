using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public interface IJobRepository
{
    Task AddAsync(JobRecord job, CancellationToken cancellationToken);

    Task<JobRecord?> GetAsync(string id, CancellationToken cancellationToken);
}
