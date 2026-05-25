namespace MSPAutomationControlPlane.Domain;

public sealed class TargetScopeTarget
{
    public required string Id { get; init; }

    public string? DisplayName { get; init; }

    public string? UserPrincipalName { get; init; }
}
