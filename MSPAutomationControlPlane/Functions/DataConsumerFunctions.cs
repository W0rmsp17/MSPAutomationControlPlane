using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Functions;

public sealed class DataConsumerFunctions(
    DataConsumerConnectorService connectorService,
    DerivedArtifactService derivedArtifactService)
{
    [Function("RegisterDataConsumerConnector")]
    public async Task<HttpResponseData> RegisterDataConsumerConnector(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "data-consumers")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var connector = await request.ReadJsonAsync<DataConsumerConnector>(cancellationToken);
        if (connector is null)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, "Request body is required.");
        }

        var result = await connectorService.RegisterAsync(connector, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, result.Error);
        }

        return await request.WriteJsonAsync(HttpStatusCode.Created, result.Value!);
    }

    [Function("ListDataConsumerConnectors")]
    public async Task<HttpResponseData> ListDataConsumerConnectors(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "data-consumers")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var connectors = await connectorService.ListAsync(cancellationToken);
        return await request.WriteJsonAsync(HttpStatusCode.OK, connectors);
    }

    [Function("ProcessJobArtifact")]
    public async Task<HttpResponseData> ProcessJobArtifact(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "jobs/{jobId}/artifacts/{artifactName}/process")] HttpRequestData request,
        string jobId,
        string artifactName,
        CancellationToken cancellationToken)
    {
        var processRequest = await request.ReadJsonAsync<ProcessArtifactRequest>(cancellationToken);
        if (processRequest is null)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, "Request body is required.");
        }

        var result = await derivedArtifactService.ProcessAsync(jobId, artifactName, processRequest, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, result.Error, result.Errors);
        }

        return await request.WriteJsonAsync(HttpStatusCode.Created, result.Value!);
    }

    [Function("ListDerivedArtifacts")]
    public async Task<HttpResponseData> ListDerivedArtifacts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobs/{jobId}/derived-artifacts")] HttpRequestData request,
        string jobId,
        CancellationToken cancellationToken)
    {
        var artifacts = await derivedArtifactService.ListByJobAsync(jobId, cancellationToken);
        return await request.WriteJsonAsync(HttpStatusCode.OK, artifacts);
    }

    [Function("GetDerivedArtifact")]
    public async Task<HttpResponseData> GetDerivedArtifact(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobs/{jobId}/derived-artifacts/{artifactId}")] HttpRequestData request,
        string jobId,
        string artifactId,
        CancellationToken cancellationToken)
    {
        var result = await derivedArtifactService.GetAsync(jobId, artifactId, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.NotFound, result.Error, result.Errors);
        }

        return await request.WriteJsonAsync(HttpStatusCode.OK, result.Value!);
    }
}
