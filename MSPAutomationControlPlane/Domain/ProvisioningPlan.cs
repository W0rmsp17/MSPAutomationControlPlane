namespace MSPAutomationControlPlane.Domain;

public sealed record ProvisioningPlan
{
    public required string ClientConnectionId { get; init; }

    public required string ClientDisplayName { get; init; }

    public required string TenantId { get; init; }

    public required string ModuleId { get; init; }

    public required string ModuleVersion { get; init; }

    public required bool IsExecutionReady { get; init; }

    public IReadOnlyList<string> BlockingIssues { get; init; } = [];

    public IReadOnlyList<ProvisioningPlanStep> Steps { get; init; } = [];

    public IReadOnlyList<RequiredPermission> RequiredPermissions { get; init; } = [];

    public string? RecommendedCertificateReference { get; init; }
}

public sealed record ProvisioningPlanStep
{
    public required int Order { get; init; }

    public required ProvisioningPlanStepStatus Status { get; init; }

    public required string Title { get; init; }

    public required string Detail { get; init; }

    public string? Owner { get; init; }
}

public enum ProvisioningPlanStepStatus
{
    Complete,
    Required,
    Blocked,
    Manual
}
