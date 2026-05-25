using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Functions;

public sealed class JobFunctions(JobService jobService)
{
    [Function("SubmitJob")]
    public async Task<HttpResponseData> SubmitJob(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "jobs")] HttpRequestData request,
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
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "jobs/{id}")] HttpRequestData request,
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
}
