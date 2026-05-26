using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Services;

public interface IModuleExecutionProvider
{
    Task<ModuleExecutionResult> ExecuteAsync(
        JobRecord job,
        string actor,
        CancellationToken cancellationToken);
}
