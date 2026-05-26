namespace MSPAutomationControlPlane.Services;

public sealed class ArtifactStorageOptions
{
    public required string ConnectionString { get; init; }

    public required string ContainerName { get; init; }

    public string? BlobServiceUri { get; init; }

    public static ArtifactStorageOptions FromEnvironment()
    {
        return new ArtifactStorageOptions
        {
            ConnectionString = Environment.GetEnvironmentVariable("ControlPlane__StorageConnectionString") ??
                Environment.GetEnvironmentVariable("AzureWebJobsStorage") ??
                throw new InvalidOperationException("ControlPlane__StorageConnectionString or AzureWebJobsStorage is required for artifact storage."),
            ContainerName = Environment.GetEnvironmentVariable("Artifacts__ContainerName") ?? "artifacts",
            BlobServiceUri = Environment.GetEnvironmentVariable("Artifacts__BlobServiceUri")
        };
    }
}
