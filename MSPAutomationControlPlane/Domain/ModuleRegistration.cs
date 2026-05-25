namespace MSPAutomationControlPlane.Domain;

public sealed class ModuleRegistration
{
    public required ModuleManifest Manifest { get; init; }

    public required string RegisteredBy { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }
}
