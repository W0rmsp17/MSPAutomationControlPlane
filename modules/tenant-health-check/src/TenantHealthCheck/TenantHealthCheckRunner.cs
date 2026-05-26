namespace TenantHealthCheck;

public static class TenantHealthCheckRunner
{
    public static ModuleJobOutput Run(ModuleJobInput input, DateTimeOffset checkedAt)
    {
        var targetCount = input.TargetScope.Targets.Count;
        var findings = new List<Finding>
        {
            new()
            {
                Severity = "Info",
                Code = "JOB_INPUT_OK",
                Title = "Job input parsed",
                Detail = $"Module '{input.ModuleId}' received job '{input.JobId}' for client connection '{input.ClientConnectionId}'."
            },
            new()
            {
                Severity = "Info",
                Code = "TARGET_SCOPE_OK",
                Title = "Target scope accepted",
                Detail = $"Scope '{input.TargetScope.Type}' with mode '{input.TargetScope.Mode}' contains {targetCount} selected target(s)."
            }
        };

        return new ModuleJobOutput
        {
            JobId = input.JobId,
            Status = "Succeeded",
            Summary = "Tenant health check completed in local contract-validation mode.",
            Findings = findings,
            Metrics = new Dictionary<string, object>
            {
                ["targetCount"] = targetCount,
                ["checkedAt"] = checkedAt
            }
        };
    }
}
