using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Functions;

public sealed class ModuleFunctions(ModuleRegistryService moduleRegistryService)
{
    [Function("RegisterModule")]
    public async Task<HttpResponseData> RegisterModule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "modules")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var manifest = await request.ReadJsonAsync<ModuleManifest>(cancellationToken);
        if (manifest is null)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, "Request body is required.");
        }

        var result = await moduleRegistryService.RegisterAsync(manifest, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(
                HttpStatusCode.BadRequest,
                "Module manifest validation failed.",
                result.Errors);
        }

        return await request.WriteJsonAsync(HttpStatusCode.Created, result.Value!);
    }

    [Function("ImportModule")]
    public async Task<HttpResponseData> ImportModule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "modules/import")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var importRequest = await request.ReadJsonAsync<ModuleImportRequest>(cancellationToken);
        if (importRequest is null)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, "Request body is required.");
        }

        var result = await moduleRegistryService.ImportAsync(importRequest, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(
                HttpStatusCode.BadRequest,
                "Module import failed.",
                result.Errors);
        }

        return await request.WriteJsonAsync(HttpStatusCode.Created, result.Value!);
    }

    [Function("ListModules")]
    public async Task<HttpResponseData> ListModules(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "modules")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var modules = await moduleRegistryService.ListAsync(cancellationToken);
        return await request.WriteJsonAsync(HttpStatusCode.OK, modules);
    }
}
