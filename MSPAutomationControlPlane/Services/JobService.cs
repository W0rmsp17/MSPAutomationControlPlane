using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Queues;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class JobService(
    IJobRepository jobRepository,
    IModuleRepository moduleRepository,
    IClientConnectionRepository clientConnectionRepository,
    IJobQueue jobQueue,
    AuditService auditService,
    IOperatorContext operatorContext,
    ReadinessService readinessService)
{
    public async Task<Result<JobRecord>> SubmitAsync(
        SubmitJobRequest request,
        CancellationToken cancellationToken)
    {
        var module = string.IsNullOrWhiteSpace(request.ModuleVersion)
            ? await moduleRepository.GetLatestAsync(request.ModuleId, cancellationToken)
            : await moduleRepository.GetAsync(request.ModuleId, request.ModuleVersion, cancellationToken);

        if (module is null)
        {
            return Result<JobRecord>.Failure($"Module '{request.ModuleId}' was not found.");
        }

        var clientConnection = await clientConnectionRepository.GetAsync(request.ClientConnectionId, cancellationToken);
        if (clientConnection is null)
        {
            return Result<JobRecord>.Failure($"Client connection '{request.ClientConnectionId}' was not found.");
        }

        var readiness = await readinessService.CheckAsync(
            new ReadinessCheckRequest
            {
                ClientConnectionId = request.ClientConnectionId,
                ModuleId = request.ModuleId,
                ModuleVersion = request.ModuleVersion,
                TargetScopeType = request.TargetScope.Type
            },
            cancellationToken);
        if (!readiness.Succeeded)
        {
            return Result<JobRecord>.Failure(readiness.Errors);
        }

        if (!readiness.Value!.IsReady)
        {
            return Result<JobRecord>.Failure(readiness.Value.BlockingIssues);
        }

        if (request.TargetScope.Mode == TargetScopeMode.Selected && request.TargetScope.Targets.Count == 0)
        {
            return Result<JobRecord>.Failure("At least one target is required when target scope mode is Selected.");
        }

        var now = DateTimeOffset.UtcNow;
        var job = new JobRecord
        {
            Id = $"job-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}",
            ModuleId = module.Manifest.Id,
            ModuleVersion = module.Manifest.Version,
            TenantContext = new TenantContext
            {
                ClientId = clientConnection.Id,
                TenantId = clientConnection.TenantId,
                TenantName = clientConnection.DisplayName
            },
            TargetScope = request.TargetScope,
            Parameters = request.Parameters,
            RequestedBy = operatorContext.CurrentOperator,
            Status = JobStatus.Queued,
            CreatedAt = now,
            UpdatedAt = now
        };

        job.Events.Add(new JobEvent("Submitted", now, "Job request accepted by the control plane.", operatorContext.CurrentOperator));
        job.Events.Add(new JobEvent("Validated", now, "Module, target scope, and request shape validated.", operatorContext.CurrentOperator));
        job.Events.Add(new JobEvent("Queued", now, "Job dispatch message queued.", operatorContext.CurrentOperator));

        await jobRepository.AddAsync(job, cancellationToken);
        await jobQueue.EnqueueAsync(
            new JobDispatchMessage
            {
                JobId = job.Id,
                ModuleId = job.ModuleId,
                ModuleVersion = job.ModuleVersion,
                ClientConnectionId = clientConnection.Id,
                QueuedAt = now
            },
            cancellationToken);

        await auditService.WriteAsync(
            AuditEventType.JobSubmitted,
            operatorContext.CurrentOperator,
            $"Job '{job.Id}' was submitted for module '{job.ModuleId}' against client connection '{clientConnection.Id}'.",
            cancellationToken,
            clientConnectionId: clientConnection.Id,
            moduleId: job.ModuleId,
            jobId: job.Id,
            resourceId: job.Id);

        return Result<JobRecord>.Success(job);
    }

    public Task<JobRecord?> GetAsync(string id, CancellationToken cancellationToken)
    {
        return jobRepository.GetAsync(id, cancellationToken);
    }

    public Task<IReadOnlyCollection<JobRecord>> ListAsync(CancellationToken cancellationToken)
    {
        return jobRepository.ListAsync(cancellationToken);
    }
}
