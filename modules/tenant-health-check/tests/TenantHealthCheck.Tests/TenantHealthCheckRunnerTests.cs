using TenantHealthCheck;

namespace TenantHealthCheck.Tests;

public sealed class TenantHealthCheckRunnerTests
{
    [Fact]
    public void Run_ReturnsSucceededOutputWithTargetMetrics()
    {
        var input = new ModuleJobInput
        {
            JobId = "job-test",
            ModuleId = "tenant-health-check",
            ModuleVersion = "0.1.1",
            ClientConnectionId = "client-contoso",
            RequestedBy = new RequestedBy
            {
                UserId = "operator-id",
                DisplayName = "Operator",
                Upn = "operator@example.com"
            },
            TargetScope = new TargetScope
            {
                Type = "Users",
                Mode = "Selected",
                Targets =
                [
                    new TargetScopeTarget
                    {
                        Id = "alex@example.com",
                        DisplayName = "Alex Example",
                        Upn = "alex@example.com"
                    }
                ]
            }
        };

        var output = TenantHealthCheckRunner.Run(input, DateTimeOffset.Parse("2026-05-26T00:00:00Z"));

        Assert.Equal("Succeeded", output.Status);
        Assert.Equal("job-test", output.JobId);
        Assert.Equal(1, output.Metrics["targetCount"]);
        Assert.Contains(output.Findings, finding => finding.Code == "JOB_INPUT_OK");
        Assert.Contains(output.Findings, finding => finding.Code == "TARGET_SCOPE_OK");
    }
}
