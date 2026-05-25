using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Http;

namespace MSPAutomationControlPlane.Functions;

public sealed class HealthFunctions
{
    [Function("GetHealth")]
    public static Task<HttpResponseData> GetHealth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData request)
    {
        return request.WriteJsonAsync(HttpStatusCode.OK, new
        {
            status = "Healthy",
            runtime = "Azure Functions isolated",
            checkedAt = DateTimeOffset.UtcNow
        });
    }
}
