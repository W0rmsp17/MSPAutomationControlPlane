namespace MSPAutomationControlPlane.Domain;

public sealed record JobEvent(
    string EventType,
    DateTimeOffset OccurredAt,
    string Message,
    string Actor);
