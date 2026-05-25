using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using MSPAutomationControlPlane.Http;

namespace MSPAutomationControlPlane.Security;

public sealed class EntraAuthorizationMiddleware(EntraTokenValidator tokenValidator) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var request = await context.GetHttpRequestDataAsync();
        if (request is null)
        {
            await next(context);
            return;
        }

        if (string.Equals(request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        request.Headers.TryGetValues("Authorization", out var values);
        var result = await tokenValidator.ValidateAsync(values?.FirstOrDefault(), context.CancellationToken);
        if (result.Succeeded)
        {
            await next(context);
            return;
        }

        var response = await request.WriteProblemAsync(HttpStatusCode.Unauthorized, result.Error ?? "Unauthorized.");
        context.GetInvocationResult().Value = response;
    }
}
