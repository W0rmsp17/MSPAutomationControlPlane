using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Functions;

public sealed class AuditFunctions(AuditService auditService)
{
    [Function("ListAuditEvents")]
    public async Task<HttpResponseData> ListAuditEvents(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "audit-events")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var auditEvents = await auditService.ListAsync(cancellationToken);
        return await request.WriteJsonAsync(HttpStatusCode.OK, auditEvents);
    }
}
