namespace MSPAutomationControlPlane.Domain;

public sealed record ConfiguredPermission
{
    public required string Provider { get; init; }

    public required string Permission { get; init; }

    public required string Type { get; init; }

    public bool AdminConsented { get; init; }
}
