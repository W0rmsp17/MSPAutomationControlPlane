namespace MSPAutomationControlPlane.Services;

public static class CertificateReferenceResolver
{
    public static bool TryResolveCertificateName(
        string certificateReference,
        out string? certificateName,
        out string? error)
    {
        certificateName = null;
        error = null;
        var trimmedReference = certificateReference.Trim();

        if (Uri.TryCreate(trimmedReference, UriKind.Absolute, out var absoluteUri))
        {
            if (string.Equals(absoluteUri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                certificateName = TryResolveKeyVaultCertificateName(absoluteUri, trimmedReference, out error);
                return certificateName is not null;
            }

            if (string.Equals(absoluteUri.Scheme, "kv", StringComparison.OrdinalIgnoreCase))
            {
                certificateName = TryResolveLogicalCertificateName(absoluteUri, trimmedReference, out error);
                return certificateName is not null;
            }
        }

        if (IsValidKeyVaultName(trimmedReference))
        {
            certificateName = trimmedReference;
            return true;
        }

        error = $"Certificate reference '{certificateReference}' is not runtime-resolvable. Use a Key Vault certificate URI, 'kv://certificates/<name>', or a Key Vault certificate name.";
        return false;
    }

    private static string? TryResolveKeyVaultCertificateName(Uri uri, string originalReference, out string? error)
    {
        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var certificateIndex = Array.FindIndex(segments, segment =>
            string.Equals(segment, "certificates", StringComparison.OrdinalIgnoreCase));

        if (certificateIndex >= 0 && certificateIndex + 1 < segments.Length)
        {
            error = null;
            return Uri.UnescapeDataString(segments[certificateIndex + 1]);
        }

        error = $"Certificate reference '{originalReference}' must point to a Key Vault certificate URI.";
        return null;
    }

    private static string? TryResolveLogicalCertificateName(Uri uri, string originalReference, out string? error)
    {
        var name = uri.Host;
        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (string.Equals(name, "certificates", StringComparison.OrdinalIgnoreCase) &&
            segments.Length == 1 &&
            IsValidKeyVaultName(segments[0]))
        {
            error = null;
            return segments[0];
        }

        if (segments.Length == 0 && IsValidKeyVaultName(name))
        {
            error = null;
            return name;
        }

        error = $"Certificate reference '{originalReference}' is a logical reference, not a Key Vault certificate name. Use 'kv://certificates/<name>' after provisioning access.";
        return null;
    }

    private static bool IsValidKeyVaultName(string value)
    {
        return value.Length is >= 1 and <= 127 &&
            value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    }
}
