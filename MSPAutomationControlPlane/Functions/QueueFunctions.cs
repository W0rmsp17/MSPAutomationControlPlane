using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Queues;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Functions;

public sealed class QueueFunctions(
    IJobQueue jobQueue,
    LocalJobDispatcher localJobDispatcher)
{
    [Function("ListLocalJobQueue")]
    public async Task<HttpResponseData> ListLocalJobQueue(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "local/job-queue")] HttpRequestData request)
    {
        if (jobQueue is not InMemoryJobQueue inMemoryJobQueue)
        {
            return await request.WriteProblemAsync(
                HttpStatusCode.BadRequest,
                "Local job queue snapshot is only available when using the in-memory queue provider.");
        }

        return await request.WriteJsonAsync(HttpStatusCode.OK, inMemoryJobQueue.Snapshot());
    }

    [Function("DispatchNextLocalJob")]
    public async Task<HttpResponseData> DispatchNextLocalJob(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "local/dispatch-next")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var result = await localJobDispatcher.DispatchNextAsync(cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, result.Error);
        }

        return await request.WriteJsonAsync(HttpStatusCode.OK, result.Value!);
    }
}
