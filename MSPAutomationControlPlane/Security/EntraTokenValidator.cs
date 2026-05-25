using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MSPAutomationControlPlane.Security;

public sealed class EntraTokenValidator
{
    private readonly ControlPlaneAuthOptions options;
    private readonly ConfigurationManager<OpenIdConnectConfiguration>? configurationManager;
    private readonly JwtSecurityTokenHandler tokenHandler = new();

    public EntraTokenValidator(ControlPlaneAuthOptions options)
    {
        this.options = options;

        if (options.Enabled && !string.IsNullOrWhiteSpace(options.TenantId))
        {
            var metadataAddress = $"https://login.microsoftonline.com/{options.TenantId}/v2.0/.well-known/openid-configuration";
            configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever());
        }
    }

    public async Task<TokenValidationResult> ValidateAsync(string? authorizationHeader, CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return TokenValidationResult.Success(new ClaimsPrincipal(new ClaimsIdentity("disabled")));
        }

        if (string.IsNullOrWhiteSpace(options.TenantId) || string.IsNullOrWhiteSpace(options.Audience))
        {
            return TokenValidationResult.Failure("API authentication is enabled but tenant ID or audience is not configured.");
        }

        if (configurationManager is null)
        {
            return TokenValidationResult.Failure("OpenID Connect metadata is not configured.");
        }

        var token = ReadBearerToken(authorizationHeader);
        if (token is null)
        {
            return TokenValidationResult.Failure("Missing bearer token.");
        }

        try
        {
            var configuration = await configurationManager.GetConfigurationAsync(cancellationToken);
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers =
                [
                    $"https://login.microsoftonline.com/{options.TenantId}/v2.0",
                    $"https://sts.windows.net/{options.TenantId}/"
                ],
                ValidateAudience = true,
                ValidAudiences = [options.Audience, StripApiScheme(options.Audience)],
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            if (!HasRequiredScope(principal))
            {
                return TokenValidationResult.Failure($"Token does not include required scope '{options.RequiredScope}'.");
            }

            if (!IsAuthorizedOperator(principal))
            {
                return TokenValidationResult.Failure("Token is valid, but the user is not authorized for this control plane.");
            }

            return TokenValidationResult.Success(principal);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or InvalidOperationException)
        {
            return TokenValidationResult.Failure("Bearer token validation failed.");
        }
    }

    private bool HasRequiredScope(ClaimsPrincipal principal)
    {
        if (string.IsNullOrWhiteSpace(options.RequiredScope))
        {
            return true;
        }

        return principal.FindAll("scp")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Contains(options.RequiredScope, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsAuthorizedOperator(ClaimsPrincipal principal)
    {
        if (options.AllowedUserObjectIds.Count == 0 && options.AllowedGroupIds.Count == 0 && options.AllowedRoles.Count == 0)
        {
            return true;
        }

        var userObjectId = principal.FindFirstValue("oid");
        if (!string.IsNullOrWhiteSpace(userObjectId) && options.AllowedUserObjectIds.Contains(userObjectId))
        {
            return true;
        }

        if (principal.FindAll("groups").Any(claim => options.AllowedGroupIds.Contains(claim.Value)))
        {
            return true;
        }

        return principal.FindAll("roles").Any(claim => options.AllowedRoles.Contains(claim.Value));
    }

    private static string? ReadBearerToken(string? authorizationHeader)
    {
        const string prefix = "Bearer ";
        return authorizationHeader is not null && authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[prefix.Length..].Trim()
            : null;
    }

    private static string StripApiScheme(string audience)
    {
        return audience.StartsWith("api://", StringComparison.OrdinalIgnoreCase)
            ? audience["api://".Length..]
            : audience;
    }
}
