using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class JobResultCollector(
    ArtifactStorageOptions options,
    IJobRepository jobRepository,
    AuditService auditService,
    IOperatorContext operatorContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<Result<JobRecord>> CollectAsync(string jobId, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetAsync(jobId, cancellationToken);
        if (job is null)
        {
            return Result<JobRecord>.Failure($"Job '{jobId}' was not found.");
        }

        if (job.Status is not JobStatus.Running)
        {
            return Result<JobRecord>.Failure($"Job '{job.Id}' is not running and cannot collect module output.");
        }

        var blobClient = new BlobContainerClient(options.ConnectionString, options.ContainerName)
            .GetBlobClient(GetResultBlobName(job.Id));

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return Result<JobRecord>.Failure($"No module output artifact found for job '{job.Id}'.");
        }

        await using var content = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        var output = await JsonSerializer.DeserializeAsync<JsonElement>(content, JsonOptions, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        job.Output = output;
        job.Status = JobStatus.Succeeded;
        job.UpdatedAt = now;
        job.Events.Add(new JobEvent("OutputCollected", now, "Module output artifact collected from blob storage.", operatorContext.CurrentOperator));
        job.Events.Add(new JobEvent("Succeeded", now, "Job completed after module output collection.", operatorContext.CurrentOperator));

        await jobRepository.UpdateAsync(job, cancellationToken);
        await auditService.WriteAsync(
            AuditEventType.JobCompleted,
            operatorContext.CurrentOperator,
            $"Job '{job.Id}' completed after module output collection.",
            cancellationToken,
            clientConnectionId: job.TenantContext.ClientId,
            moduleId: job.ModuleId,
            jobId: job.Id,
            resourceId: job.Id);

        return Result<JobRecord>.Success(job);
    }

    public static string GetResultBlobName(string jobId) => $"jobs/{jobId}/result.json";
}
