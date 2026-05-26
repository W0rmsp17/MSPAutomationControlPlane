namespace MSPAutomationControlPlane.Domain;

public sealed class RequiredPermission
{
    public required string Provider { get; init; }

    public required string Permission { get; init; }

    public required string Type { get; init; }

    public string? Reason { get; init; }
}
