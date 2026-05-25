using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Functions;

public sealed class ClientConnectionFunctions(ClientConnectionService clientConnectionService)
{
    [Function("RegisterClientConnection")]
    public async Task<HttpResponseData> RegisterClientConnection(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "client-connections")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var clientConnection = await request.ReadJsonAsync<ClientConnection>(cancellationToken);
        if (clientConnection is null)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, "Request body is required.");
        }

        var result = await clientConnectionService.RegisterAsync(clientConnection, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, result.Error);
        }

        return await request.WriteJsonAsync(HttpStatusCode.Created, result.Value!);
    }

    [Function("ListClientConnections")]
    public async Task<HttpResponseData> ListClientConnections(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "client-connections")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var connections = await clientConnectionService.ListAsync(cancellationToken);
        return await request.WriteJsonAsync(HttpStatusCode.OK, connections);
    }
}
