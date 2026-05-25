namespace MSPAutomationControlPlane.Domain;

public sealed record NotificationSubscription
{
    public string? Id { get; init; }

    public required string Name { get; init; }

    public required Uri Url { get; init; }

    public IReadOnlyList<NotificationEventType> EventTypes { get; init; } = [];

    public bool Enabled { get; init; } = true;

    public string? SigningSecretReference { get; init; }

    public string? CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
