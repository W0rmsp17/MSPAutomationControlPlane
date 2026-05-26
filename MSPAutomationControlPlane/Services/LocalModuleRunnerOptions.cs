namespace MSPAutomationControlPlane.Services;

public sealed class LocalModuleRunnerOptions
{
    public bool Enabled { get; init; }

    public string ModulesRoot { get; init; } = string.Empty;

    public string WorkRoot { get; init; } = string.Empty;

    public static LocalModuleRunnerOptions FromEnvironment()
    {
        return new LocalModuleRunnerOptions
        {
            Enabled = ReadBool("ControlPlane__LocalModules__Enabled"),
            ModulesRoot = Read("ControlPlane__LocalModules__Root"),
            WorkRoot = Read("ControlPlane__LocalModules__WorkRoot")
        };
    }

    private static string Read(string name)
    {
        return Environment.GetEnvironmentVariable(name)?.Trim() ?? string.Empty;
    }

    private static bool ReadBool(string name)
    {
        return bool.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value;
    }
}
