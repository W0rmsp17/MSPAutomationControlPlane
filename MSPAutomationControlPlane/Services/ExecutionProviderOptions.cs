namespace MSPAutomationControlPlane.Services;

public sealed class ExecutionProviderOptions
{
    public string Provider { get; init; } = "LocalOrSimulated";

    public static ExecutionProviderOptions FromEnvironment()
    {
        return new ExecutionProviderOptions
        {
            Provider = Environment.GetEnvironmentVariable("ControlPlane__ExecutionProvider") ?? "LocalOrSimulated"
        };
    }
}
