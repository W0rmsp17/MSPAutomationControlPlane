using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Queues;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class LocalJobDispatcher(
    IJobQueue jobQueue,
    JobDispatcher jobDispatcher)
{
    public async Task<Result<JobRecord>> DispatchNextAsync(CancellationToken cancellationToken)
    {
        if (jobQueue is not InMemoryJobQueue inMemoryJobQueue)
        {
            return Result<JobRecord>.Failure("Local dispatch is only available with the in-memory queue provider.");
        }

        if (!inMemoryJobQueue.TryDequeue(out var message) || message is null)
        {
            return Result<JobRecord>.Failure("No local job dispatch messages are queued.");
        }

        return await jobDispatcher.DispatchAsync(message, "local-dispatcher", cancellationToken);
    }
}
