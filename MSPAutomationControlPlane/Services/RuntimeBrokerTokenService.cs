using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class RuntimeBrokerTokenService(
    RuntimeBrokerTokenOptions options,
    IJobRepository jobRepository,
    IClientConnectionRepository clientConnectionRepository)
{
    private static readonly TokenRequestContext GraphTokenContext = new(["https://graph.microsoft.com/.default"]);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TokenCredential _controlPlaneCredential = new DefaultAzureCredential();

    public Result<RuntimeBrokerToken> Issue(JobRecord job, ModuleManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return Result<RuntimeBrokerToken>.Failure(
                "ControlPlane__RuntimeBroker__SigningKey is required to issue runtime broker tokens.");
        }

        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddSeconds(Math.Max(manifest.TimeoutSeconds, 300) + options.ExpiryBufferSeconds);
        var claims = new RuntimeBrokerTokenClaims
        {
            JobId = job.Id,
            ModuleId = job.ModuleId,
            ModuleVersion = job.ModuleVersion,
            ClientConnectionId = job.TenantContext.ClientId,
            IssuedAtUnixTime = issuedAt.ToUnixTimeSeconds(),
            ExpiresAtUnixTime = expiresAt.ToUnixTimeSeconds()
        };

        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims, JsonOptions));
        var signature = Sign(payload);

        return Result<RuntimeBrokerToken>.Success(new RuntimeBrokerToken($"{payload}.{signature}", expiresAt));
    }

    public async Task<Result<GraphAccessTokenResponse>> RedeemGraphTokenAsync(
        string? runtimeToken,
        CancellationToken cancellationToken)
    {
        var claimsResult = await ValidateAsync(runtimeToken, cancellationToken);
        if (!claimsResult.Succeeded)
        {
            return Result<GraphAccessTokenResponse>.Failure(claimsResult.Errors);
        }

        var claims = claimsResult.Value!;
        var clientConnection = await clientConnectionRepository.GetAsync(claims.ClientConnectionId, cancellationToken);
        if (clientConnection is null)
        {
            return Result<GraphAccessTokenResponse>.Failure(
                $"Client connection '{claims.ClientConnectionId}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(clientConnection.ExecutionAppClientId))
        {
            return Result<GraphAccessTokenResponse>.Failure(
                $"Client connection '{clientConnection.Id}' does not have an execution app client ID.");
        }

        if (string.IsNullOrWhiteSpace(clientConnection.CertificateReference))
        {
            return Result<GraphAccessTokenResponse>.Failure(
                $"Client connection '{clientConnection.Id}' does not have a certificate reference.");
        }

        if (string.IsNullOrWhiteSpace(options.KeyVaultUri))
        {
            return Result<GraphAccessTokenResponse>.Failure(
                "KeyVault__Uri or ControlPlane__KeyVault__Uri is required to mint execution tokens.");
        }

        if (!CertificateReferenceResolver.TryResolveCertificateName(
            clientConnection.CertificateReference,
            out var certificateName,
            out var error))
        {
            return Result<GraphAccessTokenResponse>.Failure(error!);
        }

        X509Certificate2 certificate;
        try
        {
            var certificateClient = new CertificateClient(new Uri(options.KeyVaultUri), _controlPlaneCredential);
            certificate = await certificateClient.DownloadCertificateAsync(certificateName, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<GraphAccessTokenResponse>.Failure(
                $"Could not load certificate '{certificateName}' for client connection '{clientConnection.Id}': {ex.Message}");
        }

        if (!certificate.HasPrivateKey)
        {
            return Result<GraphAccessTokenResponse>.Failure(
                $"Certificate '{certificateName}' for client connection '{clientConnection.Id}' does not include a private key.");
        }

        try
        {
            var clientCredential = new ClientCertificateCredential(
                clientConnection.TenantId,
                clientConnection.ExecutionAppClientId,
                certificate);
            var graphToken = await clientCredential.GetTokenAsync(GraphTokenContext, cancellationToken);

            return Result<GraphAccessTokenResponse>.Success(
                new GraphAccessTokenResponse(graphToken.Token, graphToken.ExpiresOn));
        }
        catch (Exception ex)
        {
            return Result<GraphAccessTokenResponse>.Failure(
                $"Could not mint Microsoft Graph token for client connection '{clientConnection.Id}': {ex.Message}");
        }
    }

    private async Task<Result<RuntimeBrokerTokenClaims>> ValidateAsync(
        string? runtimeToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return Result<RuntimeBrokerTokenClaims>.Failure(
                "ControlPlane__RuntimeBroker__SigningKey is required to validate runtime broker tokens.");
        }

        if (string.IsNullOrWhiteSpace(runtimeToken))
        {
            return Result<RuntimeBrokerTokenClaims>.Failure("Missing runtime broker token.");
        }

        var parts = runtimeToken.Split('.', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return Result<RuntimeBrokerTokenClaims>.Failure("Runtime broker token is malformed.");
        }

        var expectedSignature = Sign(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedSignature),
            Encoding.ASCII.GetBytes(parts[1])))
        {
            return Result<RuntimeBrokerTokenClaims>.Failure("Runtime broker token signature is invalid.");
        }

        RuntimeBrokerTokenClaims? claims;
        try
        {
            claims = JsonSerializer.Deserialize<RuntimeBrokerTokenClaims>(Base64UrlDecode(parts[0]), JsonOptions);
        }
        catch (JsonException)
        {
            return Result<RuntimeBrokerTokenClaims>.Failure("Runtime broker token payload is invalid.");
        }

        if (claims is null)
        {
            return Result<RuntimeBrokerTokenClaims>.Failure("Runtime broker token payload is empty.");
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > claims.ExpiresAtUnixTime)
        {
            return Result<RuntimeBrokerTokenClaims>.Failure("Runtime broker token has expired.");
        }

        var job = await jobRepository.GetAsync(claims.JobId, cancellationToken);
        if (job is null)
        {
            return Result<RuntimeBrokerTokenClaims>.Failure($"Job '{claims.JobId}' was not found.");
        }

        if (!string.Equals(job.ModuleId, claims.ModuleId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(job.ModuleVersion, claims.ModuleVersion, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(job.TenantContext.ClientId, claims.ClientConnectionId, StringComparison.OrdinalIgnoreCase))
        {
            return Result<RuntimeBrokerTokenClaims>.Failure("Runtime broker token does not match the job execution context.");
        }

        return Result<RuntimeBrokerTokenClaims>.Success(claims);
    }

    private string Sign(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.SigningKey!));
        return Base64UrlEncode(hmac.ComputeHash(Encoding.ASCII.GetBytes(payload)));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}

public sealed class RuntimeBrokerTokenOptions
{
    public string? SigningKey { get; init; }

    public string? BaseUrl { get; init; }

    public int ExpiryBufferSeconds { get; init; } = 900;

    public string? KeyVaultUri { get; init; }

    public static RuntimeBrokerTokenOptions FromEnvironment()
    {
        return new RuntimeBrokerTokenOptions
        {
            SigningKey = Environment.GetEnvironmentVariable("ControlPlane__RuntimeBroker__SigningKey"),
            BaseUrl = ReadBaseUrl(),
            ExpiryBufferSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("ControlPlane__RuntimeBroker__ExpiryBufferSeconds"),
                out var expiryBufferSeconds)
                ? expiryBufferSeconds
                : 900,
            KeyVaultUri = Environment.GetEnvironmentVariable("ControlPlane__KeyVault__Uri") ??
                Environment.GetEnvironmentVariable("KeyVault__Uri")
        };
    }

    private static string? ReadBaseUrl()
    {
        var configured = Environment.GetEnvironmentVariable("ControlPlane__RuntimeBroker__BaseUrl");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        var websiteHostName = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
        return string.IsNullOrWhiteSpace(websiteHostName)
            ? null
            : $"https://{websiteHostName}/api";
    }
}

public sealed class RuntimeBrokerTokenClaims
{
    public required string JobId { get; init; }

    public required string ModuleId { get; init; }

    public required string ModuleVersion { get; init; }

    public required string ClientConnectionId { get; init; }

    public long IssuedAtUnixTime { get; init; }

    public long ExpiresAtUnixTime { get; init; }
}

public sealed record RuntimeBrokerToken(string Token, DateTimeOffset ExpiresAt);

public sealed record GraphAccessTokenResponse(string AccessToken, DateTimeOffset ExpiresOn);
