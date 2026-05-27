using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class DerivedArtifactService(
    IDataConsumerConnectorRepository connectorRepository,
    IDerivedArtifactRepository derivedArtifactRepository,
    JobArtifactService jobArtifactService,
    AuditService auditService,
    IOperatorContext operatorContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task<Result<DerivedArtifact>> ProcessAsync(
        string jobId,
        string artifactName,
        ProcessArtifactRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(artifactName, "result", StringComparison.OrdinalIgnoreCase))
        {
            return Result<DerivedArtifact>.Failure("Only the 'result' artifact can be processed in the MVP.");
        }

        var connector = await connectorRepository.GetAsync(request.ConnectorId, cancellationToken);
        if (connector is null)
        {
            return Result<DerivedArtifact>.Failure($"Data consumer connector '{request.ConnectorId}' was not found.");
        }

        if (!connector.Enabled)
        {
            return Result<DerivedArtifact>.Failure($"Data consumer connector '{connector.Id}' is disabled.");
        }

        var source = await jobArtifactService.GetResultAsync(jobId, cancellationToken);
        if (!source.Succeeded)
        {
            return Result<DerivedArtifact>.Failure(source.Errors);
        }

        var sourceJson = JsonSerializer.Serialize(source.Value, JsonOptions);
        var sourceBytes = Encoding.UTF8.GetBytes(sourceJson);
        if (sourceBytes.Length > connector.Policy.MaxInputBytes)
        {
            return Result<DerivedArtifact>.Failure(
                $"Source artifact is {sourceBytes.Length} bytes, which exceeds connector limit {connector.Policy.MaxInputBytes} bytes.");
        }

        var now = DateTimeOffset.UtcNow;
        var derivedArtifact = new DerivedArtifact
        {
            Id = $"derived-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}",
            JobId = jobId,
            SourceArtifact = new SourceArtifactReference(
                "module-output",
                $"jobs/{jobId}/artifacts/result",
                ComputeSha256(sourceBytes)),
            Connector = new DerivedArtifactConnector(
                connector.Id!,
                connector.Type,
                connector.Provider),
            PromptTemplate = string.IsNullOrWhiteSpace(request.PromptTemplateId ?? connector.PromptTemplateId)
                ? null
                : new PromptTemplateReference(request.PromptTemplateId ?? connector.PromptTemplateId!, null),
            CreatedAt = now,
            CreatedBy = operatorContext.CurrentOperator,
            Output = BuildDerivedOutput(source.Value, connector, request)
        };

        await derivedArtifactRepository.AddAsync(derivedArtifact, cancellationToken);
        await auditService.WriteAsync(
            AuditEventType.DerivedArtifactCreated,
            operatorContext.CurrentOperator,
            $"Derived artifact '{derivedArtifact.Id}' was created for job '{jobId}'.",
            cancellationToken,
            jobId: jobId,
            resourceId: derivedArtifact.Id);

        return Result<DerivedArtifact>.Success(derivedArtifact);
    }

    public Task<IReadOnlyList<DerivedArtifact>> ListByJobAsync(string jobId, CancellationToken cancellationToken)
    {
        return derivedArtifactRepository.ListByJobAsync(jobId, cancellationToken);
    }

    public async Task<Result<DerivedArtifact>> GetAsync(
        string jobId,
        string artifactId,
        CancellationToken cancellationToken)
    {
        var artifact = await derivedArtifactRepository.GetAsync(jobId, artifactId, cancellationToken);
        return artifact is null
            ? Result<DerivedArtifact>.Failure($"Derived artifact '{artifactId}' was not found for job '{jobId}'.")
            : Result<DerivedArtifact>.Success(artifact);
    }

    private static JsonElement BuildDerivedOutput(
        JsonElement source,
        DataConsumerConnector connector,
        ProcessArtifactRequest request)
    {
        var metrics = TryGetProperty(source, "metrics");
        var findings = TryGetProperty(source, "findings");
        var recommendations = TryGetProperty(TryGetProperty(source, "report"), "recommendations");
        var licenseSummary = TryGetProperty(TryGetProperty(source, "report"), "licenseSummary");

        var output = new
        {
            schemaVersion = "1.0",
            kind = connector.Type == DataConsumerConnectorType.AI ? "ai-ready-summary" : "template-summary",
            summary = GetString(source, "summary") ?? "Module output processed.",
            metrics = new
            {
                licenseSkuCount = GetMetric(metrics, "licenseSkuCount"),
                totalLicenses = GetMetric(metrics, "totalLicenses"),
                assignedLicenses = GetMetric(metrics, "assignedLicenses"),
                availableLicenses = GetMetric(metrics, "availableLicenses"),
                usersChecked = GetMetric(metrics, "usersChecked"),
                disabledLicensedUsers = GetMetric(metrics, "disabledLicensedUsers"),
                recommendationCount = GetMetric(metrics, "recommendationCount")
            },
            accountManagerTalkingPoints = BuildTalkingPoints(metrics, recommendations),
            risks = BuildRisks(findings),
            chartData = new
            {
                licenseUsage = new[]
                {
                    new { label = "Assigned", value = GetMetric(metrics, "assignedLicenses") },
                    new { label = "Available", value = GetMetric(metrics, "availableLicenses") }
                },
                products = ExtractLicenseProducts(licenseSummary)
            },
            assumptions = new[]
            {
                "Derived output is generated from the stored module artifact only.",
                "Raw module output remains the source of truth.",
                "No direct Microsoft Graph access was used by the data consumer."
            },
            parameters = request.Parameters
        };

        return JsonSerializer.SerializeToElement(output, JsonOptions);
    }

    private static IReadOnlyList<string> BuildTalkingPoints(JsonElement metrics, JsonElement recommendations)
    {
        var points = new List<string>();
        var totalLicenses = GetMetric(metrics, "totalLicenses");
        var availableLicenses = GetMetric(metrics, "availableLicenses");
        var disabledLicensedUsers = GetMetric(metrics, "disabledLicensedUsers");

        if (totalLicenses is not null)
        {
            points.Add($"Review current license posture across {totalLicenses} total seats.");
        }

        if (availableLicenses is not null)
        {
            points.Add($"Confirm whether {availableLicenses} available seat(s) are needed for upcoming onboarding or renewal planning.");
        }

        if (disabledLicensedUsers is not null && disabledLicensedUsers > 0)
        {
            points.Add($"Review {disabledLicensedUsers} disabled licensed user(s) for possible license reclamation.");
        }

        foreach (var recommendation in EnumerateArray(recommendations).Take(3))
        {
            var action = GetString(recommendation, "recommendedAction");
            if (!string.IsNullOrWhiteSpace(action))
            {
                points.Add(action);
            }
        }

        return points.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<object> BuildRisks(JsonElement findings)
    {
        return EnumerateArray(findings)
            .Where(item => string.Equals(GetString(item, "severity"), "Warning", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetString(item, "severity"), "Error", StringComparison.OrdinalIgnoreCase))
            .Select(item => new
            {
                severity = GetString(item, "severity"),
                code = GetString(item, "code"),
                title = GetString(item, "title"),
                detail = GetString(item, "detail")
            })
            .Cast<object>()
            .ToArray();
    }

    private static IReadOnlyList<object> ExtractLicenseProducts(JsonElement licenseSummary)
    {
        var items = TryGetProperty(licenseSummary, "items");
        return EnumerateArray(items)
            .Select(item => new
            {
                product = GetString(item, "displayName") ?? GetString(item, "skuPartNumber") ?? "Unknown",
                total = GetMetric(item, "totalLicenses"),
                assigned = GetMetric(item, "assignedLicenses"),
                available = GetMetric(item, "availableLicenses")
            })
            .Cast<object>()
            .ToArray();
    }

    private static JsonElement TryGetProperty(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property)
            ? property
            : default;
    }

    private static string? GetString(JsonElement element, string name)
    {
        var property = TryGetProperty(element, name);
        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private static int? GetMetric(JsonElement element, string name)
    {
        var property = TryGetProperty(element, name);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static IEnumerable<JsonElement> EnumerateArray(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray()
            : [];
    }

    private static string ComputeSha256(byte[] bytes)
    {
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }
}
