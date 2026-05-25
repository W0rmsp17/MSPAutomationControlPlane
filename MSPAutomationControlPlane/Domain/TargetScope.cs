namespace MSPAutomationControlPlane.Domain;

public sealed class TargetScope
{
    public required TargetScopeType Type { get; init; }

    public TargetScopeMode Mode { get; init; } = TargetScopeMode.Selected;

    public IReadOnlyList<TargetScopeTarget> Targets { get; init; } = [];
}
