using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public interface IExecutionTokenBroker
{
    Task<Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>> CreateEnvironmentAsync(
        JobRecord job,
        ModuleManifest manifest,
        CancellationToken cancellationToken);
}

public sealed record ModuleRuntimeEnvironmentVariable(string Name, string Value);

public sealed class ExecutionTokenBroker(
    ExecutionTokenBrokerOptions options,
    IClientConnectionRepository clientConnectionRepository)
    : IExecutionTokenBroker
{
    private static readonly TokenRequestContext GraphTokenContext = new(["https://graph.microsoft.com/.default"]);
    private readonly TokenCredential _controlPlaneCredential = new DefaultAzureCredential();

    public async Task<Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>> CreateEnvironmentAsync(
        JobRecord job,
        ModuleManifest manifest,
        CancellationToken cancellationToken)
    {
        if (!RequiresMicrosoftGraph(manifest))
        {
            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Success([]);
        }

        var clientConnection = await clientConnectionRepository.GetAsync(job.TenantContext.ClientId, cancellationToken);
        if (clientConnection is null)
        {
            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Failure(
                $"Client connection '{job.TenantContext.ClientId}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(clientConnection.ExecutionAppClientId))
        {
            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Failure(
                $"Client connection '{clientConnection.Id}' does not have an execution app client ID.");
        }

        if (string.IsNullOrWhiteSpace(clientConnection.CertificateReference))
        {
            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Failure(
                $"Client connection '{clientConnection.Id}' does not have a certificate reference.");
        }

        if (string.IsNullOrWhiteSpace(options.KeyVaultUri))
        {
            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Failure(
                "KeyVault__Uri or ControlPlane__KeyVault__Uri is required to mint execution tokens.");
        }

        if (!CertificateReferenceResolver.TryResolveCertificateName(
            clientConnection.CertificateReference,
            out var certificateName,
            out var error))
        {
            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Failure(error!);
        }

        X509Certificate2 certificate;
        try
        {
            var certificateClient = new CertificateClient(new Uri(options.KeyVaultUri), _controlPlaneCredential);
            certificate = await certificateClient.DownloadCertificateAsync(certificateName, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Failure(
                $"Could not load certificate '{certificateName}' for client connection '{clientConnection.Id}': {ex.Message}");
        }

        if (!certificate.HasPrivateKey)
        {
            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Failure(
                $"Certificate '{certificateName}' for client connection '{clientConnection.Id}' does not include a private key.");
        }

        try
        {
            var clientCredential = new ClientCertificateCredential(
                clientConnection.TenantId,
                clientConnection.ExecutionAppClientId,
                certificate);
            var graphToken = await clientCredential.GetTokenAsync(GraphTokenContext, cancellationToken);

            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Success(
            [
                new ModuleRuntimeEnvironmentVariable("GRAPH_ACCESS_TOKEN", graphToken.Token)
            ]);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Failure(
                $"Could not mint Microsoft Graph token for client connection '{clientConnection.Id}': {ex.Message}");
        }
    }

    private static bool RequiresMicrosoftGraph(ModuleManifest manifest)
    {
        return manifest.RequiredPermissions.Any(permission =>
            string.Equals(permission.Provider, "MicrosoftGraph", StringComparison.OrdinalIgnoreCase));
    }

}

public sealed class ExecutionTokenBrokerOptions
{
    public string? KeyVaultUri { get; init; }

    public static ExecutionTokenBrokerOptions FromEnvironment()
    {
        return new ExecutionTokenBrokerOptions
        {
            KeyVaultUri = Environment.GetEnvironmentVariable("ControlPlane__KeyVault__Uri") ??
                Environment.GetEnvironmentVariable("KeyVault__Uri")
        };
    }
}
