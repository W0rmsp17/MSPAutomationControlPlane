using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class JobService(
    IJobRepository jobRepository,
    IModuleRepository moduleRepository,
    IOperatorContext operatorContext)
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

        if (!module.Manifest.SupportedScopes.Contains(request.TargetScope.Type))
        {
            return Result<JobRecord>.Failure(
                $"Module '{request.ModuleId}' does not support target scope '{request.TargetScope.Type}'.");
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
            TenantContext = request.TenantContext,
            TargetScope = request.TargetScope,
            Parameters = request.Parameters,
            RequestedBy = operatorContext.CurrentOperator,
            Status = JobStatus.Queued,
            CreatedAt = now,
            UpdatedAt = now
        };

        job.Events.Add(new JobEvent("Submitted", now, "Job request accepted by the control plane.", operatorContext.CurrentOperator));
        job.Events.Add(new JobEvent("Validated", now, "Module, target scope, and request shape validated.", operatorContext.CurrentOperator));
        job.Events.Add(new JobEvent("Queued", now, "Job marked as queued in local MVP mode.", operatorContext.CurrentOperator));
        job.Events.Add(new JobEvent("DispatchSkippedForMvp", now, "Service Bus and Container Apps dispatch are not wired yet.", "control-plane"));

        await jobRepository.AddAsync(job, cancellationToken);
        return Result<JobRecord>.Success(job);
    }

    public Task<JobRecord?> GetAsync(string id, CancellationToken cancellationToken)
    {
        return jobRepository.GetAsync(id, cancellationToken);
    }
}
