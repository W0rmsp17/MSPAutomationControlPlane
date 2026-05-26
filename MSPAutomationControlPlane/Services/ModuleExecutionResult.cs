using System.Text.Json;

namespace MSPAutomationControlPlane.Services;

public sealed record ModuleExecutionResult(
    bool Succeeded,
    JsonElement? Output,
    string StartMessage,
    string CompletionMessage,
    string? SkipMessage = null)
{
    public static ModuleExecutionResult SimulatedSuccess(string skipMessage)
        => new(
            true,
            null,
            "Simulated worker started.",
            "Simulated worker completed successfully.",
            skipMessage);

    public static ModuleExecutionResult Success(JsonElement? output)
        => new(
            true,
            output,
            "Module worker started.",
            "Module worker completed successfully.");

    public static ModuleExecutionResult Failure(string error)
        => new(
            false,
            null,
            "Module worker started.",
            error);
}
