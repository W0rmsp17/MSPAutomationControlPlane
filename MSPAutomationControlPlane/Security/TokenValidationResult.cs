using System.Security.Claims;

namespace MSPAutomationControlPlane.Security;

public sealed record TokenValidationResult(bool Succeeded, ClaimsPrincipal? Principal, string? Error)
{
    public static TokenValidationResult Success(ClaimsPrincipal principal) => new(true, principal, null);

    public static TokenValidationResult Failure(string error) => new(false, null, error);
}
