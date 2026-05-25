namespace MSPAutomationControlPlane.Queues;

public sealed class ServiceBusQueueOptions
{
    public required string ConnectionString { get; init; }

    public string JobQueueName { get; init; } = "jobs";

    public static ServiceBusQueueOptions FromEnvironment()
    {
        var connectionString = Environment.GetEnvironmentVariable("ControlPlane__ServiceBusConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ServiceBus queue provider requires ControlPlane__ServiceBusConnectionString.");
        }

        return new ServiceBusQueueOptions
        {
            ConnectionString = connectionString,
            JobQueueName = Environment.GetEnvironmentVariable("ControlPlane__JobQueueName") ?? "jobs"
        };
    }
}
