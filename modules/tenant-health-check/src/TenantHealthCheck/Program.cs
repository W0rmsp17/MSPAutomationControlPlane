using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using TenantHealthCheck;

var inputPath = Environment.GetEnvironmentVariable("CONTROL_PLANE_INPUT_PATH");
var outputPath = Environment.GetEnvironmentVariable("CONTROL_PLANE_OUTPUT_PATH");
var inputBase64 = Environment.GetEnvironmentVariable("CONTROL_PLANE_JOB_INPUT_BASE64");
var outputBlobUri = Environment.GetEnvironmentVariable("CONTROL_PLANE_OUTPUT_BLOB_URI");

if (string.IsNullOrWhiteSpace(inputPath) && string.IsNullOrWhiteSpace(inputBase64))
{
    Console.Error.WriteLine("CONTROL_PLANE_INPUT_PATH or CONTROL_PLANE_JOB_INPUT_BASE64 is required.");
    return 2;
}

try
{
    var inputJson = string.IsNullOrWhiteSpace(inputPath)
        ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(inputBase64!))
        : await File.ReadAllTextAsync(inputPath);

    var input = JsonSerializer.Deserialize<ModuleJobInput>(inputJson, ModuleJson.Options);
    if (input is null)
    {
        Console.Error.WriteLine("Input file could not be deserialized.");
        return 2;
    }

    var output = TenantHealthCheckRunner.Run(input, DateTimeOffset.UtcNow);
    var outputJson = JsonSerializer.Serialize(output, ModuleJson.Options);

    if (!string.IsNullOrWhiteSpace(outputBlobUri))
    {
        var blobClient = new BlobClient(new Uri(outputBlobUri), new DefaultAzureCredential());
        using var uploadContent = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(outputJson));
        await blobClient.UploadAsync(uploadContent, overwrite: true);
    }

    if (string.IsNullOrWhiteSpace(outputPath))
    {
        Console.WriteLine(outputJson);
        return 0;
    }

    var outputDirectory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    await File.WriteAllTextAsync(outputPath, outputJson);
    return 0;
}
catch (FormatException ex)
{
    Console.Error.WriteLine($"CONTROL_PLANE_JOB_INPUT_BASE64 is not valid base64: {ex.Message}");
    return 2;
}
catch (JsonException ex)
{
    Console.Error.WriteLine($"Input file is not valid JSON: {ex.Message}");
    return 2;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Could not write module output: {ex.Message}");
    return 3;
}
catch (RequestFailedException ex)
{
    Console.Error.WriteLine($"Could not upload module output: {ex.Message}");
    return 3;
}
