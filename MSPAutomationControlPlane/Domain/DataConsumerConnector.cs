namespace MSPAutomationControlPlane.Domain;

public sealed record DataConsumerConnector
{
    public string? Id { get; init; }

    public required string DisplayName { get; init; }

    public DataConsumerConnectorType Type { get; init; } = DataConsumerConnectorType.TemplateSummary;

    public bool Enabled { get; init; } = true;

    public string? Provider { get; init; }

    public string? PromptTemplateId { get; init; }

    public DataConsumerConnectorPolicy Policy { get; init; } = new();

    public string? CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public enum DataConsumerConnectorType
{
    TemplateSummary,
    AI,
    Webhook,
    StorageExport,
    PowerBI,
    EmailRenderer
}

public sealed record DataConsumerConnectorPolicy
{
    public bool RequiresManualRun { get; init; } = true;

    public bool StorePrompt { get; init; } = false;

    public bool StoreResponse { get; init; } = true;

    public bool AllowPersonalData { get; init; } = true;

    public int MaxInputBytes { get; init; } = 262144;
}
