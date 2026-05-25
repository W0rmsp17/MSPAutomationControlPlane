using System.Text.Json;

namespace MSPAutomationControlPlane.Domain;

public sealed class ModuleManifest
{
    public string SchemaVersion { get; init; } = "1.0";

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public string? Description { get; init; }

    public required string Image { get; init; }

    public string Runtime { get; init; } = "container-apps-job";

    public int TimeoutSeconds { get; init; } = 900;

    public int Concurrency { get; init; } = 1;

    public bool ApprovalRequired { get; init; }

    public IReadOnlyList<TargetScopeType> SupportedScopes { get; init; } = [TargetScopeType.Tenant];

    public JsonElement ParametersSchema { get; init; }

    public IReadOnlyList<RequiredPermission> RequiredPermissions { get; init; } = [];

    public JsonElement OutputsSchema { get; init; }
}
