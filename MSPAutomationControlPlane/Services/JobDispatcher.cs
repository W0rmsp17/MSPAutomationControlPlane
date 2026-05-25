using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class JobDispatcher(
    IJobRepository jobRepository,
    AuditService auditService)
{
    public async Task<Result<JobRecord>> DispatchAsync(
        JobDispatchMessage message,
        string actor,
        CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetAsync(message.JobId, cancellationToken);
        if (job is null)
        {
            return Result<JobRecord>.Failure($"Queued job '{message.JobId}' was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        job.Status = JobStatus.Running;
        job.UpdatedAt = now;
        job.Events.Add(new JobEvent("DispatchStarted", now, "Dispatcher picked up the job.", actor));
        job.Events.Add(new JobEvent("WorkerStarted", now, "Simulated worker started.", actor));

        var completedAt = DateTimeOffset.UtcNow;
        job.Status = JobStatus.Succeeded;
        job.UpdatedAt = completedAt;
        job.Events.Add(new JobEvent("Succeeded", completedAt, "Simulated worker completed successfully.", actor));

        await jobRepository.UpdateAsync(job, cancellationToken);
        await auditService.WriteAsync(
            AuditEventType.JobCompleted,
            actor,
            $"Job '{job.Id}' completed in simulated dispatch mode.",
            cancellationToken,
            clientConnectionId: job.TenantContext.ClientId,
            moduleId: job.ModuleId,
            jobId: job.Id,
            resourceId: job.Id);

        return Result<JobRecord>.Success(job);
    }
}
