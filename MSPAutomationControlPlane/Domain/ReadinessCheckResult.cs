namespace MSPAutomationControlPlane.Domain;

public sealed class ReadinessCheckResult
{
    public required string ClientConnectionId { get; init; }

    public required string ModuleId { get; init; }

    public string? ModuleVersion { get; init; }

    public TargetScopeType? TargetScopeType { get; init; }

    public bool IsReady => BlockingIssues.Count == 0;

    public IReadOnlyList<string> BlockingIssues { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<RequiredPermission> RequiredPermissions { get; init; } = [];

    public IReadOnlyList<ConfiguredPermission> MatchingPermissions { get; init; } = [];
}
