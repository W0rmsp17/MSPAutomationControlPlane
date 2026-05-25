using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Queues;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class LocalJobDispatcher(
    IJobQueue jobQueue,
    IJobRepository jobRepository,
    AuditService auditService)
{
    public async Task<Result<JobRecord>> DispatchNextAsync(CancellationToken cancellationToken)
    {
        if (jobQueue is not InMemoryJobQueue inMemoryJobQueue)
        {
            return Result<JobRecord>.Failure("Local dispatch is only available with the in-memory queue provider.");
        }

        if (!inMemoryJobQueue.TryDequeue(out var message) || message is null)
        {
            return Result<JobRecord>.Failure("No local job dispatch messages are queued.");
        }

        var job = await jobRepository.GetAsync(message.JobId, cancellationToken);
        if (job is null)
        {
            return Result<JobRecord>.Failure($"Queued job '{message.JobId}' was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        job.Status = JobStatus.Running;
        job.UpdatedAt = now;
        job.Events.Add(new JobEvent("DispatchStarted", now, "Local dispatcher picked up the job.", "local-dispatcher"));
        job.Events.Add(new JobEvent("WorkerStarted", now, "Local simulated worker started.", "local-dispatcher"));

        var completedAt = DateTimeOffset.UtcNow;
        job.Status = JobStatus.Succeeded;
        job.UpdatedAt = completedAt;
        job.Events.Add(new JobEvent("Succeeded", completedAt, "Local simulated worker completed successfully.", "local-dispatcher"));

        await jobRepository.UpdateAsync(job, cancellationToken);
        await auditService.WriteAsync(
            AuditEventType.JobCompleted,
            "local-dispatcher",
            $"Job '{job.Id}' completed in local dispatch mode.",
            cancellationToken,
            clientConnectionId: job.TenantContext.ClientId,
            moduleId: job.ModuleId,
            jobId: job.Id,
            resourceId: job.Id);

        return Result<JobRecord>.Success(job);
    }
}
