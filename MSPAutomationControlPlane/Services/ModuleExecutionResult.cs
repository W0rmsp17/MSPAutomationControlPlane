using System.Text.Json;

namespace MSPAutomationControlPlane.Services;

public sealed record ModuleExecutionResult(
    bool Succeeded,
    bool IsTerminal,
    JsonElement? Output,
    string StartMessage,
    string CompletionMessage,
    string? SkipMessage = null)
{
    public static ModuleExecutionResult SimulatedSuccess(string skipMessage)
        => new(
            true,
            true,
            null,
            "Simulated worker started.",
            "Simulated worker completed successfully.",
            skipMessage);

    public static ModuleExecutionResult Success(JsonElement? output)
        => new(
            true,
            true,
            output,
            "Module worker started.",
            "Module worker completed successfully.");

    public static ModuleExecutionResult Started(string message)
        => new(
            true,
            false,
            null,
            "Module worker started.",
            message);

    public static ModuleExecutionResult Failure(string error)
        => new(
            false,
            true,
            null,
            "Module worker started.",
            error);
}
