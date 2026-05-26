using System.Text.Json;

namespace TenantHealthCheck;

public sealed class ModuleJobInput
{
    public string SchemaVersion { get; init; } = "1.0";

    public required string JobId { get; init; }

    public required string ModuleId { get; init; }

    public required string ModuleVersion { get; init; }

    public required RequestedBy RequestedBy { get; init; }

    public required string ClientConnectionId { get; init; }

    public required TargetScope TargetScope { get; init; }

    public JsonElement Parameters { get; init; }
}

public sealed class RequestedBy
{
    public required string UserId { get; init; }

    public required string DisplayName { get; init; }

    public required string Upn { get; init; }
}

public sealed class TargetScope
{
    public required string Type { get; init; }

    public required string Mode { get; init; }

    public IReadOnlyList<TargetScopeTarget> Targets { get; init; } = [];
}

public sealed class TargetScopeTarget
{
    public required string Id { get; init; }

    public string? DisplayName { get; init; }

    public string? Upn { get; init; }
}
