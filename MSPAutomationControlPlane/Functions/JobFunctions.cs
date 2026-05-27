using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Functions;

public sealed class JobFunctions(
    JobService jobService,
    JobResultCollector jobResultCollector,
    JobArtifactService jobArtifactService)
{
    [Function("ListJobs")]
    public async Task<HttpResponseData> ListJobs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobs")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var jobs = await jobService.ListAsync(cancellationToken);
        return await request.WriteJsonAsync(HttpStatusCode.OK, jobs);
    }

    [Function("SubmitJob")]
    public async Task<HttpResponseData> SubmitJob(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "jobs")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var submitRequest = await request.ReadJsonAsync<SubmitJobRequest>(cancellationToken);
        if (submitRequest is null)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, "Request body is required.");
        }

        var result = await jobService.SubmitAsync(submitRequest, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, result.Error);
        }

        return await request.WriteJsonAsync(HttpStatusCode.Accepted, result.Value!);
    }

    [Function("GetJob")]
    public async Task<HttpResponseData> GetJob(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobs/{id}")] HttpRequestData request,
        string id,
        CancellationToken cancellationToken)
    {
        var job = await jobService.GetAsync(id, cancellationToken);
        if (job is null)
        {
            return await request.WriteProblemAsync(HttpStatusCode.NotFound, $"Job '{id}' was not found.");
        }

        return await request.WriteJsonAsync(HttpStatusCode.OK, job);
    }

    [Function("CollectJobResult")]
    public async Task<HttpResponseData> CollectJobResult(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "jobs/{id}/collect-result")] HttpRequestData request,
        string id,
        CancellationToken cancellationToken)
    {
        var result = await jobResultCollector.CollectAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, result.Error);
        }

        return await request.WriteJsonAsync(HttpStatusCode.OK, result.Value!);
    }

    [Function("ListJobArtifacts")]
    public async Task<HttpResponseData> ListJobArtifacts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobs/{id}/artifacts")] HttpRequestData request,
        string id,
        CancellationToken cancellationToken)
    {
        var result = await jobArtifactService.ListAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, result.Error);
        }

        return await request.WriteJsonAsync(HttpStatusCode.OK, result.Value!);
    }

    [Function("GetJobResultArtifact")]
    public async Task<HttpResponseData> GetJobResultArtifact(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobs/{id}/artifacts/result")] HttpRequestData request,
        string id,
        CancellationToken cancellationToken)
    {
        var result = await jobArtifactService.GetResultAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, result.Error);
        }

        return await request.WriteJsonAsync(HttpStatusCode.OK, result.Value!);
    }
}
