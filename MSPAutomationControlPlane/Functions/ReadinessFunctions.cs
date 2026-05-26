using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Functions;

public sealed class ReadinessFunctions(ReadinessService readinessService)
{
    [Function("CheckReadiness")]
    public async Task<HttpResponseData> CheckReadiness(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "readiness/check")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var readinessRequest = await request.ReadJsonAsync<ReadinessCheckRequest>(cancellationToken);
        if (readinessRequest is null)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, "Request body is required.");
        }

        var result = await readinessService.CheckAsync(readinessRequest, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, result.Error, result.Errors);
        }

        return await request.WriteJsonAsync(HttpStatusCode.OK, result.Value!);
    }
}
