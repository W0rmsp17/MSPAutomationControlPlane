using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Functions;

public sealed class ProvisioningFunctions(ProvisioningPlanService provisioningPlanService)
{
    [Function("CreateProvisioningPlan")]
    public async Task<HttpResponseData> CreateProvisioningPlan(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "provisioning/plan")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var provisioningRequest = await request.ReadJsonAsync<ProvisioningPlanRequest>(cancellationToken);
        if (provisioningRequest is null)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, "Request body is required.");
        }

        var result = await provisioningPlanService.CreateAsync(provisioningRequest, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, result.Error, result.Errors);
        }

        return await request.WriteJsonAsync(HttpStatusCode.OK, result.Value!);
    }
}
