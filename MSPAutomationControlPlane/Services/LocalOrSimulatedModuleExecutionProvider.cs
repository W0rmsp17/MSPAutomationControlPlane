using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Services;

public sealed class LocalOrSimulatedModuleExecutionProvider(LocalModuleRunner localModuleRunner) : IModuleExecutionProvider
{
    public async Task<ModuleExecutionResult> ExecuteAsync(
        JobRecord job,
        string actor,
        CancellationToken cancellationToken)
    {
        var moduleRun = await localModuleRunner.TryRunAsync(job, actor, cancellationToken);
        if (moduleRun.ExitCode == -1)
        {
            return ModuleExecutionResult.SimulatedSuccess(moduleRun.Message);
        }

        return moduleRun.Succeeded
            ? ModuleExecutionResult.Success(moduleRun.Output)
            : ModuleExecutionResult.Failure(moduleRun.Message);
    }
}
