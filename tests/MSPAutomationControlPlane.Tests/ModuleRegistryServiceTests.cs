using System.Net;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Tests;

public sealed class ModuleRegistryServiceTests
{
    [Fact]
    public async Task ImportAsync_Rejects_NonGithubRawManifestUrl()
    {
        var service = CreateService("{}");
        var request = ImportRequest("https://example.com/module.manifest.json");

        var result = await service.ImportAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_Rejects_HttpManifestUrl()
    {
        var service = CreateService("{}");
        var request = ImportRequest("http://raw.githubusercontent.com/owner/repo/v1.0.0/module.manifest.json");

        var result = await service.ImportAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("HTTPS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_Rejects_MovingRefManifestUrl_ByDefault()
    {
        var service = CreateService("{}");
        var request = ImportRequest("https://raw.githubusercontent.com/owner/repo/main/module.manifest.json");

        var result = await service.ImportAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("moving refs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_Accepts_PinnedGithubRawManifestUrl()
    {
        var service = CreateService(ValidManifestJson);
        var request = ImportRequest("https://raw.githubusercontent.com/owner/repo/v1.0.0/module.manifest.json");

        var result = await service.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal("tenant-health-check", result.Value!.Manifest.Id);
    }

    private static ModuleRegistryService CreateService(string responseBody)
    {
        var moduleRepository = new InMemoryModuleRepository();
        var auditRepository = new InMemoryAuditEventRepository();
        var auditService = new AuditService(auditRepository);
        var operatorContext = new TestOperatorContext();
        var validator = new ModuleManifestValidator(new ModuleRegistryOptions());
        var httpClient = new HttpClient(new StaticResponseHandler(responseBody));

        return new ModuleRegistryService(moduleRepository, validator, auditService, operatorContext, httpClient);
    }

    private static ModuleImportRequest ImportRequest(string manifestUrl) =>
        new()
        {
            Source = new ModuleImportSource
            {
                Type = "manifestUrl",
                ManifestUrl = manifestUrl
            }
        };

    private const string ValidManifestJson = """
        {
          "schemaVersion": "1.0",
          "id": "tenant-health-check",
          "name": "Tenant Health Check",
          "version": "1.0.0",
          "image": "ghcr.io/example/tenant-health-check:1.0.0",
          "runtime": "container-apps-job",
          "timeoutSeconds": 900,
          "concurrency": 1,
          "supportedScopes": ["Tenant"],
          "parametersSchema": { "type": "object" },
          "outputsSchema": { "type": "object" },
          "requiredPermissions": [
            {
              "provider": "MicrosoftGraph",
              "permission": "Organization.Read.All",
              "type": "Application"
            }
          ]
        }
        """;

    private sealed class StaticResponseHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };

            return Task.FromResult(response);
        }
    }

    private sealed class TestOperatorContext : IOperatorContext
    {
        public string CurrentOperator { get; private set; } = "unit-test";

        public void SetCurrentOperator(string? operatorName)
        {
            CurrentOperator = string.IsNullOrWhiteSpace(operatorName) ? "unit-test" : operatorName;
        }
    }
}
