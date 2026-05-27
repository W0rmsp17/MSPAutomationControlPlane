using System.Text.Json;

namespace MSPAutomationControlPlane.Domain;

public sealed record DerivedArtifact
{
    public required string Id { get; init; }

    public required string JobId { get; init; }

    public required SourceArtifactReference SourceArtifact { get; init; }

    public required DerivedArtifactConnector Connector { get; init; }

    public PromptTemplateReference? PromptTemplate { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required string CreatedBy { get; init; }

    public string Classification { get; init; } = "DerivedCustomerConfidential";

    public required JsonElement Output { get; init; }
}

public sealed record SourceArtifactReference(
    string Kind,
    string Path,
    string ContentHash);

public sealed record DerivedArtifactConnector(
    string Id,
    DataConsumerConnectorType Type,
    string? Provider);

public sealed record PromptTemplateReference(
    string Id,
    string? Version);

public sealed record ProcessArtifactRequest
{
    public required string ConnectorId { get; init; }

    public string? PromptTemplateId { get; init; }

    public Dictionary<string, JsonElement> Parameters { get; init; } = [];
}
