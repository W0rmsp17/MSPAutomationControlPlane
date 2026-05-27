using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Services;

public sealed class GraphPermissionGrantVerifier(
    GraphPermissionGrantVerifierOptions options,
    HttpClient httpClient)
{
    private static readonly TokenRequestContext GraphTokenContext = new(["https://graph.microsoft.com/.default"]);

    private readonly TokenCredential _controlPlaneCredential = new DefaultAzureCredential();

    public async Task<Result<GraphPermissionGrantCheckResult>> CheckAsync(
        ClientConnection clientConnection,
        IReadOnlyList<RequiredPermission> requiredPermissions,
        CancellationToken cancellationToken)
    {
        var graphPermissions = requiredPermissions
            .Where(permission => string.Equals(permission.Provider, "MicrosoftGraph", StringComparison.OrdinalIgnoreCase))
            .Where(permission => string.Equals(permission.Type, "Application", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (graphPermissions.Length == 0)
        {
            return Result<GraphPermissionGrantCheckResult>.Success(new GraphPermissionGrantCheckResult([], []));
        }

        if (!options.Enabled)
        {
            return Result<GraphPermissionGrantCheckResult>.Success(new GraphPermissionGrantCheckResult(
                [],
                ["Live Microsoft Graph grant validation is disabled for this environment."]));
        }

        var validationErrors = ValidateClientConnection(clientConnection);
        if (validationErrors.Count > 0)
        {
            return Result<GraphPermissionGrantCheckResult>.Failure(validationErrors);
        }

        var appRoleIds = new HashSet<Guid>();
        foreach (var permission in graphPermissions)
        {
            if (!TryGetGraphAppRoleId(permission.Permission, out var appRoleId))
            {
                return Result<GraphPermissionGrantCheckResult>.Failure(
                    $"Unknown Microsoft Graph application permission '{permission.Permission}'. Add its app role ID before live readiness validation can check it.");
            }

            appRoleIds.Add(appRoleId);
        }

        var tokenResult = await GetGraphTokenAsync(clientConnection, cancellationToken);
        if (!tokenResult.Succeeded)
        {
            return Result<GraphPermissionGrantCheckResult>.Failure(tokenResult.Errors);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://graph.microsoft.com/v1.0/servicePrincipals/{Uri.EscapeDataString(clientConnection.ServicePrincipalObjectId!)}/appRoleAssignments?$select=appRoleId,resourceDisplayName");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Result<GraphPermissionGrantCheckResult>.Failure(
                $"Could not verify target app Graph grants for client connection '{clientConnection.Id}': {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var grantedRoleIds = ParseGrantedAppRoleIds(body);
        var missingPermissions = graphPermissions
            .Where(permission => TryGetGraphAppRoleId(permission.Permission, out var roleId) && !grantedRoleIds.Contains(roleId))
            .Select(permission => permission.Permission)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Result<GraphPermissionGrantCheckResult>.Success(new GraphPermissionGrantCheckResult(missingPermissions, []));
    }

    private static List<string> ValidateClientConnection(ClientConnection clientConnection)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(clientConnection.ExecutionAppClientId))
        {
            errors.Add($"Client connection '{clientConnection.Id}' does not have an execution app client ID.");
        }

        if (string.IsNullOrWhiteSpace(clientConnection.ServicePrincipalObjectId))
        {
            errors.Add($"Client connection '{clientConnection.Id}' does not have a target service principal object ID.");
        }

        if (string.IsNullOrWhiteSpace(clientConnection.CertificateReference))
        {
            errors.Add($"Client connection '{clientConnection.Id}' does not have a certificate reference.");
        }

        return errors;
    }

    private async Task<Result<string>> GetGraphTokenAsync(
        ClientConnection clientConnection,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.KeyVaultUri))
        {
            return Result<string>.Failure(
                "KeyVault__Uri or ControlPlane__KeyVault__Uri is required for live Microsoft Graph grant validation.");
        }

        if (!CertificateReferenceResolver.TryResolveCertificateName(
            clientConnection.CertificateReference!,
            out var certificateName,
            out var error))
        {
            return Result<string>.Failure(error!);
        }

        X509Certificate2 certificate;
        try
        {
            var certificateClient = new CertificateClient(new Uri(options.KeyVaultUri), _controlPlaneCredential);
            certificate = await certificateClient.DownloadCertificateAsync(certificateName, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(
                $"Could not load certificate '{certificateName}' for client connection '{clientConnection.Id}': {ex.Message}");
        }

        if (!certificate.HasPrivateKey)
        {
            return Result<string>.Failure(
                $"Certificate '{certificateName}' for client connection '{clientConnection.Id}' does not include a private key.");
        }

        try
        {
            var clientCredential = new ClientCertificateCredential(
                clientConnection.TenantId,
                clientConnection.ExecutionAppClientId,
                certificate);
            var graphToken = await clientCredential.GetTokenAsync(GraphTokenContext, cancellationToken);
            return Result<string>.Success(graphToken.Token);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(
                $"Could not mint Microsoft Graph token for live readiness validation on client connection '{clientConnection.Id}': {ex.Message}");
        }
    }

    private static HashSet<Guid> ParseGrantedAppRoleIds(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.TryGetProperty("resourceDisplayName", out var resourceDisplayName) &&
                string.Equals(resourceDisplayName.GetString(), "Microsoft Graph", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.TryGetProperty("appRoleId", out var appRoleId) && Guid.TryParse(appRoleId.GetString(), out var parsed)
                ? parsed
                : Guid.Empty)
            .Where(roleId => roleId != Guid.Empty)
            .ToHashSet();
    }

    private static bool TryGetGraphAppRoleId(string permission, out Guid appRoleId)
    {
        var knownGraphApplicationRoles = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["Directory.Read.All"] = Guid.Parse("7ab1d382-f21e-4acd-a863-ba3e13f7da61"),
            ["Organization.Read.All"] = Guid.Parse("498476ce-e0fe-48b0-b801-37ba7e2685c6"),
            ["User.Read.All"] = Guid.Parse("df021288-bdef-4463-88db-98f22de89214")
        };

        return knownGraphApplicationRoles.TryGetValue(permission, out appRoleId);
    }
}

public sealed class GraphPermissionGrantVerifierOptions
{
    public bool Enabled { get; init; }

    public string? KeyVaultUri { get; init; }

    public static GraphPermissionGrantVerifierOptions FromEnvironment()
    {
        return new GraphPermissionGrantVerifierOptions
        {
            Enabled = bool.TryParse(
                Environment.GetEnvironmentVariable("ControlPlane__Readiness__LiveGraphValidationEnabled"),
                out var enabled) && enabled,
            KeyVaultUri = Environment.GetEnvironmentVariable("ControlPlane__KeyVault__Uri") ??
                Environment.GetEnvironmentVariable("KeyVault__Uri")
        };
    }
}

public sealed record GraphPermissionGrantCheckResult(
    IReadOnlyList<string> MissingPermissions,
    IReadOnlyList<string> Warnings);
