using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Queues;

namespace MSPAutomationControlPlane.Functions;

public sealed class QueueFunctions(IJobQueue jobQueue)
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
}
