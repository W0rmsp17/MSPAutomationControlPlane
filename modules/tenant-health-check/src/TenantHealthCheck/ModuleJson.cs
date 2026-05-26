using System.Text.Json;
using System.Text.Json.Serialization;

namespace TenantHealthCheck;

public static class ModuleJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };
}
