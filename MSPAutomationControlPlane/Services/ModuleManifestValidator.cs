using System.Text.RegularExpressions;
using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Services;

public sealed partial class ModuleManifestValidator(ModuleRegistryOptions options)
{
    private static readonly HashSet<string> SupportedRuntimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "container-apps-job"
    };

    private static readonly HashSet<string> SupportedPermissionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Application",
        "Delegated"
    };

    public ModuleManifestValidationResult Validate(ModuleManifest manifest)
    {
        var errors = new List<string>();

        ValidateRequiredText(errors, manifest.Id, "id");
        ValidateRequiredText(errors, manifest.Name, "name");
        ValidateRequiredText(errors, manifest.Version, "version");
        ValidateRequiredText(errors, manifest.Image, "image");

        if (!string.IsNullOrWhiteSpace(manifest.Id) && !ModuleIdPattern().IsMatch(manifest.Id))
        {
            errors.Add("id must contain only lowercase letters, numbers, dots, and hyphens.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.Version) && !SemVerPattern().IsMatch(manifest.Version))
        {
            errors.Add("version must use semantic version format, such as 1.0.0.");
        }

        if (!SupportedRuntimes.Contains(manifest.Runtime))
        {
            errors.Add($"runtime '{manifest.Runtime}' is not supported. Supported values: {string.Join(", ", SupportedRuntimes)}.");
        }

        if (manifest.SupportedScopes.Count == 0)
        {
            errors.Add("supportedScopes must include at least one target scope.");
        }

        if (manifest.TimeoutSeconds is < 30 or > 7200)
        {
            errors.Add("timeoutSeconds must be between 30 and 7200.");
        }

        if (manifest.Concurrency is < 1 or > 50)
        {
            errors.Add("concurrency must be between 1 and 50.");
        }

        ValidateImage(errors, manifest.Image);
        ValidateJsonSchema(errors, manifest.ParametersSchema, "parametersSchema");
        ValidateJsonSchema(errors, manifest.OutputsSchema, "outputsSchema");
        ValidateOptionalJsonObject(errors, manifest.ExecutionContract, "executionContract");
        ValidateDataHandling(errors, manifest.DataHandling);
        ValidateRequiredPermissions(errors, manifest.RequiredPermissions);

        return errors.Count == 0
            ? ModuleManifestValidationResult.Success
            : new ModuleManifestValidationResult(errors);
    }

    private void ValidateImage(List<string> errors, string? image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return;
        }

        if (!image.Contains('/') || image.Contains(" ", StringComparison.Ordinal))
        {
            errors.Add("image must be a container image reference including a registry host.");
            return;
        }

        var registry = image.Split('/')[0];
        if (options.AllowedRegistries.Count > 0 && !options.AllowedRegistries.Contains(registry))
        {
            errors.Add($"image registry '{registry}' is not allowed. Allowed registries: {string.Join(", ", options.AllowedRegistries)}.");
        }
    }

    private static void ValidateJsonSchema(List<string> errors, System.Text.Json.JsonElement schema, string fieldName)
    {
        if (schema.ValueKind is System.Text.Json.JsonValueKind.Undefined or System.Text.Json.JsonValueKind.Null)
        {
            errors.Add($"{fieldName} is required.");
            return;
        }

        if (schema.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            errors.Add($"{fieldName} must be a JSON object.");
            return;
        }

        if (schema.TryGetProperty("type", out var type) &&
            type.ValueKind == System.Text.Json.JsonValueKind.String &&
            !string.Equals(type.GetString(), "object", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{fieldName}.type must be 'object' for the MVP.");
        }
    }

    private static void ValidateOptionalJsonObject(List<string> errors, System.Text.Json.JsonElement value, string fieldName)
    {
        if (value.ValueKind is System.Text.Json.JsonValueKind.Undefined or System.Text.Json.JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            errors.Add($"{fieldName} must be a JSON object.");
        }
    }

    private static void ValidateDataHandling(List<string> errors, DataHandlingMetadata? dataHandling)
    {
        if (dataHandling is null)
        {
            return;
        }

        ValidateRequiredText(errors, dataHandling.Classification, "dataHandling.classification");

        if (dataHandling.ContainsPersonalData is null)
        {
            errors.Add("dataHandling.containsPersonalData is required when dataHandling is supplied.");
        }

        if (dataHandling.ContainsSecrets is null)
        {
            errors.Add("dataHandling.containsSecrets is required when dataHandling is supplied.");
        }

        if (dataHandling.RetentionRecommendationDays is < 1)
        {
            errors.Add("dataHandling.retentionRecommendationDays must be at least 1 when supplied.");
        }
    }

    private static void ValidateRequiredPermissions(List<string> errors, IReadOnlyList<RequiredPermission> requiredPermissions)
    {
        if (requiredPermissions.Count == 0)
        {
            errors.Add("requiredPermissions must declare at least one permission.");
            return;
        }

        for (var index = 0; index < requiredPermissions.Count; index++)
        {
            var permission = requiredPermissions[index];
            var prefix = $"requiredPermissions[{index}]";

            ValidateRequiredText(errors, permission.Provider, $"{prefix}.provider");
            ValidateRequiredText(errors, permission.Permission, $"{prefix}.permission");
            ValidateRequiredText(errors, permission.Type, $"{prefix}.type");

            if (!string.IsNullOrWhiteSpace(permission.Type) && !SupportedPermissionTypes.Contains(permission.Type))
            {
                errors.Add($"{prefix}.type must be Application or Delegated.");
            }
        }
    }

    private static void ValidateRequiredText(List<string> errors, string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,62}[a-z0-9]$")]
    private static partial Regex ModuleIdPattern();

    [GeneratedRegex("^\\d+\\.\\d+\\.\\d+([-+][0-9A-Za-z.-]+)?$")]
    private static partial Regex SemVerPattern();
}
