using System.Text.Json;

namespace MSPAutomationControlPlane.Domain;

public sealed class JobRecord
{
    public required string Id { get; init; }

    public required string ModuleId { get; init; }

    public required string ModuleVersion { get; init; }

    public required TenantContext TenantContext { get; init; }

    public required TargetScope TargetScope { get; init; }

    public JsonElement Parameters { get; init; }

    public required string RequestedBy { get; init; }

    public JobStatus Status { get; set; }

    public JsonElement? Output { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<JobEvent> Events { get; init; } = [];
}
