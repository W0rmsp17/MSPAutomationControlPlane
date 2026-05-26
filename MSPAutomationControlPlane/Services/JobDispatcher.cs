using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class JobDispatcher(
    IJobRepository jobRepository,
    IModuleExecutionProvider moduleExecutionProvider,
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

        var execution = await moduleExecutionProvider.ExecuteAsync(job, actor, cancellationToken);
        var completedAt = DateTimeOffset.UtcNow;

        if (execution.SkipMessage is not null)
        {
            job.Events.Add(new JobEvent("WorkerSkipped", completedAt, execution.SkipMessage, actor));
        }

        job.Events.Add(new JobEvent("WorkerStarted", now, execution.StartMessage, actor));
        if (execution.Succeeded)
        {
            job.Output = execution.Output;
            if (execution.IsTerminal)
            {
                job.Status = JobStatus.Succeeded;
                job.Events.Add(new JobEvent("Succeeded", completedAt, execution.CompletionMessage, actor));
            }
            else
            {
                job.Status = JobStatus.Running;
                job.Events.Add(new JobEvent("ExecutionStarted", completedAt, execution.CompletionMessage, actor));
            }
        }
        else
        {
            job.Status = JobStatus.Failed;
            job.Events.Add(new JobEvent("Failed", completedAt, execution.CompletionMessage, actor));
        }

        job.UpdatedAt = completedAt;
        await jobRepository.UpdateAsync(job, cancellationToken);

        if (execution.IsTerminal)
        {
            await auditService.WriteAsync(
                job.Status == JobStatus.Succeeded ? AuditEventType.JobCompleted : AuditEventType.JobFailed,
                actor,
                $"Job '{job.Id}' completed with status '{job.Status}'.",
                cancellationToken,
                clientConnectionId: job.TenantContext.ClientId,
                moduleId: job.ModuleId,
                jobId: job.Id,
                resourceId: job.Id);
        }

        return Result<JobRecord>.Success(job);
    }
}
