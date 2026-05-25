namespace MSPAutomationControlPlane.Services;

public sealed class ModuleRegistryOptions
{
    public HashSet<string> AllowedRegistries { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "ghcr.io",
        "mcr.microsoft.com"
    };

    public static ModuleRegistryOptions FromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("ControlPlane__Modules__AllowedRegistries");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new ModuleRegistryOptions();
        }

        return new ModuleRegistryOptions
        {
            AllowedRegistries = raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
    }
}
