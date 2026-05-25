namespace MSPAutomationControlPlane.Domain;

public sealed class TenantContext
{
    public required string ClientId { get; init; }

    public required string TenantId { get; init; }

    public required string TenantName { get; init; }
}
