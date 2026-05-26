using System.Text.Json;

namespace MSPAutomationControlPlane.Services;

public sealed record LocalModuleRunResult(
    bool Succeeded,
    int ExitCode,
    JsonElement? Output,
    string Message)
{
    public static LocalModuleRunResult Skipped(string message) => new(false, -1, null, message);
}
