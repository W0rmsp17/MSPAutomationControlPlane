namespace TenantHealthCheck;

public sealed class ModuleJobOutput
{
    public string SchemaVersion { get; init; } = "1.0";

    public required string JobId { get; init; }

    public required string Status { get; init; }

    public required string Summary { get; init; }

    public IReadOnlyList<Finding> Findings { get; init; } = [];

    public Dictionary<string, object> Metrics { get; init; } = [];

    public IReadOnlyList<ArtifactReference> Artifacts { get; init; } = [];
}

public sealed class Finding
{
    public required string Severity { get; init; }

    public required string Code { get; init; }

    public required string Title { get; init; }

    public required string Detail { get; init; }
}

public sealed class ArtifactReference
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string Uri { get; init; }
}
