namespace MSPAutomationControlPlane.Domain;

public sealed record AuditEvent
{
    public required string Id { get; init; }

    public required AuditEventType EventType { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required string Actor { get; init; }

    public string? ClientConnectionId { get; init; }

    public string? ModuleId { get; init; }

    public string? JobId { get; init; }

    public string? ResourceId { get; init; }

    public required string Summary { get; init; }
}
