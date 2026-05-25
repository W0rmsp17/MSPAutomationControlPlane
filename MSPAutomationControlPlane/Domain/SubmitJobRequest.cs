using System.Text.Json;

namespace MSPAutomationControlPlane.Domain;

public sealed class SubmitJobRequest
{
    public required string ModuleId { get; init; }

    public string? ModuleVersion { get; init; }

    public required string ClientConnectionId { get; init; }

    public required TargetScope TargetScope { get; init; }

    public JsonElement Parameters { get; init; }
}
