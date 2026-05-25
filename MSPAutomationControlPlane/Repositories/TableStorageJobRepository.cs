using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class TableStorageJobRepository(TableStorageOptions options) : IJobRepository
{
    private const string PartitionKey = "JOB";
    private readonly TableJsonStore<JobRecord> _store = new(options, "Jobs");

    public Task AddAsync(JobRecord job, CancellationToken cancellationToken)
    {
        return _store.UpsertAsync(PartitionKey, job.Id, job, cancellationToken);
    }

    public Task<JobRecord?> GetAsync(string id, CancellationToken cancellationToken)
    {
        return _store.GetAsync(PartitionKey, id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<JobRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var jobs = await _store.ListPartitionAsync(PartitionKey, cancellationToken);
        return jobs
            .OrderByDescending(job => job.CreatedAt)
            .ToArray();
    }

    public Task UpdateAsync(JobRecord job, CancellationToken cancellationToken)
    {
        return _store.UpsertAsync(PartitionKey, job.Id, job, cancellationToken);
    }
}
