namespace MSPAutomationControlPlane.Domain;

public sealed class ModuleImportRequest
{
    public ModuleImportSource Source { get; init; } = new();

    public ModuleImportRegistrationOptions Registration { get; init; } = new();

    public ModuleImportValidationOptions Validation { get; init; } = new();
}

public sealed class ModuleImportSource
{
    public string Type { get; init; } = "git";

    public string? Repository { get; init; }

    public string Ref { get; init; } = "main";

    public string ManifestPath { get; init; } = "module.manifest.json";

    public string? ManifestUrl { get; init; }
}

public sealed class ModuleImportRegistrationOptions
{
    public bool Enabled { get; init; } = true;

    public string Visibility { get; init; } = "Private";

    public IReadOnlyList<string> AllowedClientConnectionIds { get; init; } = [];

    public string DefaultRunMode { get; init; } = "Standard";
}

public sealed class ModuleImportValidationOptions
{
    public bool RequireImageTagMatch { get; init; } = true;

    public bool RequirePackageValidation { get; init; } = true;
}
