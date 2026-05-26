using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSPAutomationControlPlane.Services;

public sealed class ModuleRegistryService(
    IModuleRepository moduleRepository,
    ModuleManifestValidator manifestValidator,
    AuditService auditService,
    IOperatorContext operatorContext,
    HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<Result<ModuleRegistration>> RegisterAsync(
        ModuleManifest manifest,
        CancellationToken cancellationToken)
    {
        var validation = manifestValidator.Validate(manifest);
        if (!validation.Succeeded)
        {
            return Result<ModuleRegistration>.Failure(validation.Errors);
        }

        var existing = await moduleRepository.GetAsync(manifest.Id, manifest.Version, cancellationToken);
        if (existing is not null)
        {
            return Result<ModuleRegistration>.Failure(
                $"Module '{manifest.Id}' version '{manifest.Version}' is already registered.");
        }

        var registration = new ModuleRegistration
        {
            Manifest = manifest,
            RegisteredBy = operatorContext.CurrentOperator,
            RegisteredAt = DateTimeOffset.UtcNow
        };

        await moduleRepository.AddAsync(registration, cancellationToken);
        await auditService.WriteAsync(
            AuditEventType.ModuleRegistered,
            operatorContext.CurrentOperator,
            $"Module '{manifest.Id}' version '{manifest.Version}' was registered.",
            cancellationToken,
            moduleId: manifest.Id,
            resourceId: $"{manifest.Id}:{manifest.Version}");

        return Result<ModuleRegistration>.Success(registration);
    }

    public async Task<Result<ModuleRegistration>> ImportAsync(
        ModuleImportRequest importRequest,
        CancellationToken cancellationToken)
    {
        var manifestUri = BuildManifestUri(importRequest.Source);
        if (manifestUri is null)
        {
            return Result<ModuleRegistration>.Failure("Import source must supply a manifestUrl or a GitHub repository, ref, and manifestPath.");
        }

        using var response = await httpClient.GetAsync(manifestUri, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Result<ModuleRegistration>.Failure(
                $"Manifest fetch failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        var manifest = JsonSerializer.Deserialize<ModuleManifest>(body, JsonOptions);
        if (manifest is null)
        {
            return Result<ModuleRegistration>.Failure("Fetched manifest could not be parsed.");
        }

        var sourceRepository = importRequest.Source.Repository;
        if (!string.IsNullOrWhiteSpace(sourceRepository) &&
            !string.IsNullOrWhiteSpace(manifest.Repository) &&
            !string.Equals(sourceRepository.TrimEnd('/'), manifest.Repository.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            return Result<ModuleRegistration>.Failure("Fetched manifest repository does not match import request repository.");
        }

        return await RegisterAsync(manifest, cancellationToken);
    }

    public Task<IReadOnlyList<ModuleRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        return moduleRepository.ListAsync(cancellationToken);
    }

    private static Uri? BuildManifestUri(ModuleImportSource source)
    {
        if (!string.IsNullOrWhiteSpace(source.ManifestUrl) &&
            Uri.TryCreate(source.ManifestUrl, UriKind.Absolute, out var manifestUri))
        {
            return manifestUri;
        }

        if (!string.Equals(source.Type, "git", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(source.Repository))
        {
            return null;
        }

        if (!Uri.TryCreate(source.Repository.TrimEnd('/'), UriKind.Absolute, out var repositoryUri) ||
            !string.Equals(repositoryUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = repositoryUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        var owner = Uri.EscapeDataString(parts[0]);
        var repo = Uri.EscapeDataString(parts[1]);
        var gitRef = Uri.EscapeDataString(source.Ref);
        var manifestPath = string.Join(
            '/',
            source.ManifestPath
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        return new Uri($"https://raw.githubusercontent.com/{owner}/{repo}/{gitRef}/{manifestPath}");
    }
}
