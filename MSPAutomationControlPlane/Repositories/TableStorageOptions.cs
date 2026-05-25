namespace MSPAutomationControlPlane.Repositories;

public sealed class TableStorageOptions
{
    public required string ConnectionString { get; init; }

    public string TablePrefix { get; init; } = "MspControl";

    public static TableStorageOptions FromEnvironment()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ControlPlane__StorageConnectionString") ??
            Environment.GetEnvironmentVariable("AzureWebJobsStorage");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "TableStorage repository provider requires ControlPlane__StorageConnectionString or AzureWebJobsStorage.");
        }

        return new TableStorageOptions
        {
            ConnectionString = connectionString,
            TablePrefix = Environment.GetEnvironmentVariable("ControlPlane__TablePrefix") ?? "MspControl"
        };
    }
}
