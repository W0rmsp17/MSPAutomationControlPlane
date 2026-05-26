using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Security;

public sealed class EntraAuthorizationMiddleware(
    EntraTokenValidator tokenValidator,
    IOperatorContext operatorContext) : IFunctionsWorkerMiddleware
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
            operatorContext.SetCurrentOperator(GetOperatorName(result.Principal));
            try
            {
                await next(context);
            }
            finally
            {
                operatorContext.SetCurrentOperator(null);
            }

            return;
        }

        var response = await request.WriteProblemAsync(HttpStatusCode.Unauthorized, result.Error ?? "Unauthorized.");
        context.GetInvocationResult().Value = response;
    }

    private static string? GetOperatorName(System.Security.Claims.ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        return principal.FindFirst("preferred_username")?.Value
            ?? principal.FindFirst("upn")?.Value
            ?? principal.FindFirst("unique_name")?.Value
            ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Upn)?.Value
            ?? principal.FindFirst("name")?.Value
            ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? principal.FindFirst("oid")?.Value
            ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
    }
}
