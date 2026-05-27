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
    IClientConnectionRepository clientConnectionRepository,
    RuntimeBrokerTokenService runtimeBrokerTokenService)
    : IExecutionTokenBroker
{
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

        if (!CertificateReferenceResolver.TryResolveCertificateName(
            clientConnection.CertificateReference,
            out _,
            out var error))
        {
            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Failure(error!);
        }

        if (string.IsNullOrWhiteSpace(options.RuntimeBrokerBaseUrl))
        {
            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Failure(
                "ControlPlane__RuntimeBroker__BaseUrl or WEBSITE_HOSTNAME is required to broker runtime tokens.");
        }

        var token = runtimeBrokerTokenService.Issue(job, manifest);
        if (!token.Succeeded)
        {
            return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Failure(token.Errors);
        }

        return Result<IReadOnlyList<ModuleRuntimeEnvironmentVariable>>.Success(
        [
            new ModuleRuntimeEnvironmentVariable(
                "CONTROL_PLANE_RUNTIME_TOKEN_URL",
                $"{options.RuntimeBrokerBaseUrl.TrimEnd('/')}/execution/tokens/graph"),
            new ModuleRuntimeEnvironmentVariable("CONTROL_PLANE_RUNTIME_TOKEN", token.Value!.Token),
            new ModuleRuntimeEnvironmentVariable("CONTROL_PLANE_RUNTIME_TOKEN_EXPIRES_ON", token.Value.ExpiresAt.ToString("o"))
        ]);
    }

    private static bool RequiresMicrosoftGraph(ModuleManifest manifest)
    {
        return manifest.RequiredPermissions.Any(permission =>
            string.Equals(permission.Provider, "MicrosoftGraph", StringComparison.OrdinalIgnoreCase));
    }

}

public sealed class ExecutionTokenBrokerOptions
{
    public string? RuntimeBrokerBaseUrl { get; init; }

    public static ExecutionTokenBrokerOptions FromEnvironment()
    {
        return new ExecutionTokenBrokerOptions
        {
            RuntimeBrokerBaseUrl = RuntimeBrokerTokenOptions.FromEnvironment().BaseUrl
        };
    }
}
