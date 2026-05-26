using System.Text.Json;
using System.Text.Json.Serialization;
using TenantHealthCheck;

var inputPath = Environment.GetEnvironmentVariable("CONTROL_PLANE_INPUT_PATH");
var outputPath = Environment.GetEnvironmentVariable("CONTROL_PLANE_OUTPUT_PATH");

if (string.IsNullOrWhiteSpace(inputPath))
{
    Console.Error.WriteLine("CONTROL_PLANE_INPUT_PATH is required.");
    return 2;
}

if (string.IsNullOrWhiteSpace(outputPath))
{
    Console.Error.WriteLine("CONTROL_PLANE_OUTPUT_PATH is required.");
    return 3;
}

try
{
    var inputJson = await File.ReadAllTextAsync(inputPath);
    var input = JsonSerializer.Deserialize<ModuleJobInput>(inputJson, ModuleJson.Options);
    if (input is null)
    {
        Console.Error.WriteLine("Input file could not be deserialized.");
        return 2;
    }

    var output = TenantHealthCheckRunner.Run(input, DateTimeOffset.UtcNow);
    var outputJson = JsonSerializer.Serialize(output, ModuleJson.Options);

    var outputDirectory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    await File.WriteAllTextAsync(outputPath, outputJson);
    return 0;
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
