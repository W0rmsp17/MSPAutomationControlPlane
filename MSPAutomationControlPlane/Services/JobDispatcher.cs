using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class JobDispatcher(
    IJobRepository jobRepository,
    LocalModuleRunner localModuleRunner,
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

        var moduleRun = await localModuleRunner.TryRunAsync(job, actor, cancellationToken);
        var completedAt = DateTimeOffset.UtcNow;

        if (moduleRun.ExitCode == -1)
        {
            job.Events.Add(new JobEvent("WorkerSkipped", completedAt, moduleRun.Message, actor));
            job.Events.Add(new JobEvent("WorkerStarted", completedAt, "Simulated worker started.", actor));
            job.Status = JobStatus.Succeeded;
            job.UpdatedAt = completedAt;
            job.Events.Add(new JobEvent("Succeeded", completedAt, "Simulated worker completed successfully.", actor));
        }
        else if (moduleRun.Succeeded && moduleRun.Output is not null)
        {
            job.Output = moduleRun.Output;
            job.Status = JobStatus.Succeeded;
            job.UpdatedAt = completedAt;
            job.Events.Add(new JobEvent("WorkerStarted", now, "Local module worker started.", actor));
            job.Events.Add(new JobEvent("Succeeded", completedAt, "Local module worker completed successfully.", actor));
        }
        else
        {
            job.Status = JobStatus.Failed;
            job.UpdatedAt = completedAt;
            job.Events.Add(new JobEvent("WorkerStarted", now, "Local module worker started.", actor));
            job.Events.Add(new JobEvent("Failed", completedAt, moduleRun.Message, actor));
        }

        await jobRepository.UpdateAsync(job, cancellationToken);
        await auditService.WriteAsync(
            job.Status == JobStatus.Succeeded ? AuditEventType.JobCompleted : AuditEventType.JobFailed,
            actor,
            $"Job '{job.Id}' completed with status '{job.Status}'.",
            cancellationToken,
            clientConnectionId: job.TenantContext.ClientId,
            moduleId: job.ModuleId,
            jobId: job.Id,
            resourceId: job.Id);

        return Result<JobRecord>.Success(job);
    }
}
