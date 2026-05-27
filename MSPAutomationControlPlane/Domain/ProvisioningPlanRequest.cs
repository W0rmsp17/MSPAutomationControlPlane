namespace MSPAutomationControlPlane.Domain;

public sealed record ProvisioningPlanRequest
{
    public required string ClientConnectionId { get; init; }

    public required string ModuleId { get; init; }

    public string? ModuleVersion { get; init; }
}
