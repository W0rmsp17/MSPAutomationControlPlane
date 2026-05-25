using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Queues;

public interface IJobQueue
{
    Task EnqueueAsync(JobDispatchMessage message, CancellationToken cancellationToken);
}
