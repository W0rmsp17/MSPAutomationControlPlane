namespace MSPAutomationControlPlane.Services;

public sealed class ContainerAppsExecutionOptions
{
    public required string SubscriptionId { get; init; }

    public required string ResourceGroupName { get; init; }

    public required string JobName { get; init; }

    public string ApiVersion { get; init; } = "2026-01-01";

    public string ContainerName { get; init; } = "module-worker";

    public double Cpu { get; init; } = 0.25;

    public string Memory { get; init; } = "0.5Gi";

    public static ContainerAppsExecutionOptions FromEnvironment()
    {
        return new ContainerAppsExecutionOptions
        {
            SubscriptionId = GetRequired("ControlPlane__ContainerApps__SubscriptionId"),
            ResourceGroupName = GetRequired("ControlPlane__ContainerApps__ResourceGroupName"),
            JobName = GetRequired("ControlPlane__ContainerApps__JobName"),
            ApiVersion = Environment.GetEnvironmentVariable("ControlPlane__ContainerApps__ApiVersion") ?? "2026-01-01",
            ContainerName = Environment.GetEnvironmentVariable("ControlPlane__ContainerApps__ContainerName") ?? "module-worker",
            Cpu = double.TryParse(Environment.GetEnvironmentVariable("ControlPlane__ContainerApps__Cpu"), out var cpu)
                ? cpu
                : 0.25,
            Memory = Environment.GetEnvironmentVariable("ControlPlane__ContainerApps__Memory") ?? "0.5Gi"
        };
    }

    private static string GetRequired(string name)
    {
        return Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} is required when ControlPlane__ExecutionProvider is ContainerApps.");
    }
}
