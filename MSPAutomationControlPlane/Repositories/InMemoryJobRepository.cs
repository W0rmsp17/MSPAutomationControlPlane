using System.Collections.Concurrent;
using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class InMemoryJobRepository : IJobRepository
{
    private readonly ConcurrentDictionary<string, JobRecord> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(JobRecord job, CancellationToken cancellationToken)
    {
        _jobs[job.Id] = job;
        return Task.CompletedTask;
    }

    public Task<JobRecord?> GetAsync(string id, CancellationToken cancellationToken)
    {
        _jobs.TryGetValue(id, out var job);
        return Task.FromResult(job);
    }

    public Task UpdateAsync(JobRecord job, CancellationToken cancellationToken)
    {
        _jobs[job.Id] = job;
        return Task.CompletedTask;
    }
}
