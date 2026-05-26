using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class ContainerAppsModuleExecutionProvider(
    ContainerAppsExecutionOptions options,
    ArtifactStorageOptions artifactStorageOptions,
    IModuleRepository moduleRepository,
    HttpClient httpClient) : IModuleExecutionProvider
{
    private static readonly TokenRequestContext ArmTokenContext = new(["https://management.azure.com/.default"]);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly DefaultAzureCredential _credential = new();

    public async Task<ModuleExecutionResult> ExecuteAsync(
        JobRecord job,
        string actor,
        CancellationToken cancellationToken)
    {
        var module = await moduleRepository.GetAsync(job.ModuleId, job.ModuleVersion, cancellationToken);
        if (module is null)
        {
            return ModuleExecutionResult.Failure($"Module '{job.ModuleId}' version '{job.ModuleVersion}' was not found.");
        }

        var accessToken = await _credential.GetTokenAsync(ArmTokenContext, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildStartUri());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        request.Content = new StringContent(
            JsonSerializer.Serialize(CreateStartRequest(job, actor, module.Manifest), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ModuleExecutionResult.Failure(
                $"Container Apps Job start failed with {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
        }

        var executionName = ReadExecutionName(responseBody);
        var message = string.IsNullOrWhiteSpace(executionName)
            ? $"Container Apps Job '{options.JobName}' execution was started."
            : $"Container Apps Job '{options.JobName}' execution '{executionName}' was started.";

        return ModuleExecutionResult.Started(message);
    }

    private Uri BuildStartUri()
    {
        var subscriptionId = Uri.EscapeDataString(options.SubscriptionId);
        var resourceGroupName = Uri.EscapeDataString(options.ResourceGroupName);
        var jobName = Uri.EscapeDataString(options.JobName);
        var apiVersion = Uri.EscapeDataString(options.ApiVersion);

        return new Uri(
            $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.App/jobs/{jobName}/start?api-version={apiVersion}");
    }

    private object CreateStartRequest(JobRecord job, string actor, ModuleManifest manifest)
    {
        var inputJson = JsonSerializer.Serialize(CreateModuleInput(job, actor), JsonOptions);
        var inputBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(inputJson));

        return new
        {
            containers = new[]
            {
                new
                {
                    name = options.ContainerName,
                    image = manifest.Image,
                    resources = new
                    {
                        cpu = options.Cpu,
                        memory = options.Memory
                    },
                    env = new[]
                    {
                        new { name = "CONTROL_PLANE_JOB_ID", value = job.Id },
                        new { name = "CONTROL_PLANE_MODULE_ID", value = job.ModuleId },
                        new { name = "CONTROL_PLANE_MODULE_VERSION", value = job.ModuleVersion },
                        new { name = "CONTROL_PLANE_CLIENT_CONNECTION_ID", value = job.TenantContext.ClientId },
                        new { name = "CONTROL_PLANE_REQUESTED_BY", value = actor },
                        new { name = "CONTROL_PLANE_JOB_INPUT_BASE64", value = inputBase64 },
                        new { name = "CONTROL_PLANE_OUTPUT_BLOB_URI", value = BuildResultBlobUri(job.Id, manifest.TimeoutSeconds) }
                    }
                }
            }
        };
    }

    private static object CreateModuleInput(JobRecord job, string actor)
    {
        return new
        {
            schemaVersion = "1.0",
            jobId = job.Id,
            moduleId = job.ModuleId,
            moduleVersion = job.ModuleVersion,
            requestedBy = new
            {
                userId = actor,
                displayName = actor,
                upn = actor
            },
            clientConnectionId = job.TenantContext.ClientId,
            targetScope = job.TargetScope,
            parameters = job.Parameters
        };
    }

    private string BuildResultBlobUri(string jobId, int timeoutSeconds)
    {
        var blobName = JobResultCollector.GetResultBlobName(jobId);
        var blobClient = new BlobContainerClient(artifactStorageOptions.ConnectionString, artifactStorageOptions.ContainerName)
            .GetBlobClient(blobName);

        if (blobClient.CanGenerateSasUri)
        {
            var expiry = DateTimeOffset.UtcNow.AddSeconds(Math.Max(timeoutSeconds, 300) + 900);
            return blobClient.GenerateSasUri(BlobSasPermissions.Create | BlobSasPermissions.Write, expiry).ToString();
        }

        var serviceUri = options.ArtifactBlobServiceUri.TrimEnd('/');
        var container = Uri.EscapeDataString(options.ArtifactContainerName);

        return $"{serviceUri}/{container}/{blobName}";
    }

    private static string? ReadExecutionName(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.TryGetProperty("name", out var name)
            ? name.GetString()
            : null;
    }
}
