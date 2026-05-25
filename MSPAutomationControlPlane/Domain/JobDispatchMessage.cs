namespace MSPAutomationControlPlane.Domain;

public sealed record JobDispatchMessage
{
    public required string JobId { get; init; }

    public required string ModuleId { get; init; }

    public required string ModuleVersion { get; init; }

    public required string ClientConnectionId { get; init; }

    public required DateTimeOffset QueuedAt { get; init; }
}
