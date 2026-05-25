namespace MSPAutomationControlPlane.Security;

public sealed class ControlPlaneAuthOptions
{
    public bool Enabled { get; init; }

    public string TenantId { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string RequiredScope { get; init; } = "access_as_user";

    public HashSet<string> AllowedUserObjectIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> AllowedGroupIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> AllowedRoles { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public static ControlPlaneAuthOptions FromEnvironment()
    {
        return new ControlPlaneAuthOptions
        {
            Enabled = ReadBool("ControlPlane__Auth__Enabled"),
            TenantId = Read("ControlPlane__Auth__TenantId"),
            Audience = Read("ControlPlane__Auth__Audience"),
            RequiredScope = Read("ControlPlane__Auth__RequiredScope", "access_as_user"),
            AllowedUserObjectIds = ReadSet("ControlPlane__Auth__AllowedUserObjectIds"),
            AllowedGroupIds = ReadSet("ControlPlane__Auth__AllowedGroupIds"),
            AllowedRoles = ReadSet("ControlPlane__Auth__AllowedRoles")
        };
    }

    private static string Read(string name, string defaultValue = "")
    {
        return Environment.GetEnvironmentVariable(name)?.Trim() ?? defaultValue;
    }

    private static bool ReadBool(string name)
    {
        return bool.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value;
    }

    private static HashSet<string> ReadSet(string name)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(raw)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
