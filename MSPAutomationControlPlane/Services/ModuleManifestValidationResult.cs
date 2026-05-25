namespace MSPAutomationControlPlane.Services;

public sealed record ModuleManifestValidationResult(IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;

    public static ModuleManifestValidationResult Success { get; } = new([]);
}
