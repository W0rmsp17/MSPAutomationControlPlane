using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Functions;

public sealed class RuntimeTokenFunctions(RuntimeBrokerTokenService runtimeBrokerTokenService)
{
    [Function("RedeemGraphRuntimeToken")]
    public async Task<HttpResponseData> RedeemGraphRuntimeToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "execution/tokens/graph")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryGetValues("Authorization", out var values);
        var token = ReadBearerToken(values?.FirstOrDefault());
        if (string.IsNullOrWhiteSpace(token))
        {
            request.Headers.TryGetValues("X-Control-Plane-Runtime-Token", out var runtimeTokenValues);
            token = runtimeTokenValues?.FirstOrDefault();
        }

        var result = await runtimeBrokerTokenService.RedeemGraphTokenAsync(token, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.Unauthorized, result.Error, result.Errors);
        }

        return await request.WriteJsonAsync(HttpStatusCode.OK, result.Value!);
    }

    private static string? ReadBearerToken(string? authorizationHeader)
    {
        const string prefix = "Bearer ";
        return authorizationHeader is not null && authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[prefix.Length..].Trim()
            : null;
    }
}
