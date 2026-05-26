namespace MSPAutomationControlPlane.Domain;

public sealed class ReadinessCheckRequest
{
    public required string ClientConnectionId { get; init; }

    public required string ModuleId { get; init; }

    public string? ModuleVersion { get; init; }

    public TargetScopeType? TargetScopeType { get; init; }
}
