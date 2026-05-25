namespace MSPAutomationControlPlane.Domain;

public sealed record ClientConnection
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string TenantId { get; init; }

    public ExecutionMode ExecutionMode { get; init; } = ExecutionMode.Central;

    public string? ExecutionAppClientId { get; init; }

    public string? CertificateReference { get; init; }

    public string? ServicePrincipalObjectId { get; init; }

    public ClientConnectionReadinessStatus ReadinessStatus { get; init; } = ClientConnectionReadinessStatus.Draft;

    public IReadOnlyList<ConfiguredPermission> ConfiguredPermissions { get; init; } = [];

    public DateTimeOffset? LastReadinessCheckAt { get; init; }

    public string? ReadinessNotes { get; init; }

    public IReadOnlyList<string> EnabledModuleIds { get; init; } = [];

    public IReadOnlyList<TargetScopeType> AllowedScopes { get; init; } = [TargetScopeType.Tenant];

    public bool Enabled { get; init; } = true;

    public string? CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
