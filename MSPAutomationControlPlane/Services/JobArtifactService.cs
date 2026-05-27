using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class JobArtifactService(
    ArtifactStorageOptions options,
    IJobRepository jobRepository)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<Result<IReadOnlyList<JobArtifactDescriptor>>> ListAsync(
        string jobId,
        CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetAsync(jobId, cancellationToken);
        if (job is null)
        {
            return Result<IReadOnlyList<JobArtifactDescriptor>>.Failure($"Job '{jobId}' was not found.");
        }

        var blobClient = GetResultBlobClient(jobId);
        var descriptors = new List<JobArtifactDescriptor>();
        if (await blobClient.ExistsAsync(cancellationToken))
        {
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            descriptors.Add(new JobArtifactDescriptor(
                Name: "result.json",
                Type: "application/json",
                Kind: "module-output",
                SizeBytes: properties.Value.ContentLength,
                LastModified: properties.Value.LastModified,
                Path: $"jobs/{jobId}/artifacts/result"));
        }

        return Result<IReadOnlyList<JobArtifactDescriptor>>.Success(descriptors);
    }

    public async Task<Result<JsonElement>> GetResultAsync(
        string jobId,
        CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetAsync(jobId, cancellationToken);
        if (job is null)
        {
            return Result<JsonElement>.Failure($"Job '{jobId}' was not found.");
        }

        if (job.Output is JsonElement output)
        {
            return Result<JsonElement>.Success(output);
        }

        var blobClient = GetResultBlobClient(jobId);
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return Result<JsonElement>.Failure($"No module output artifact found for job '{job.Id}'.");
        }

        await using var content = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<JsonElement>(content, JsonOptions, cancellationToken);
        return Result<JsonElement>.Success(result);
    }

    private BlobClient GetResultBlobClient(string jobId)
    {
        return new BlobContainerClient(options.ConnectionString, options.ContainerName)
            .GetBlobClient(JobResultCollector.GetResultBlobName(jobId));
    }
}

public sealed record JobArtifactDescriptor(
    string Name,
    string Type,
    string Kind,
    long SizeBytes,
    DateTimeOffset LastModified,
    string Path);
