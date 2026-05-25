using System.Collections.Concurrent;
using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Queues;

public sealed class InMemoryJobQueue : IJobQueue
{
    private readonly ConcurrentQueue<JobDispatchMessage> _messages = new();

    public Task EnqueueAsync(JobDispatchMessage message, CancellationToken cancellationToken)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<JobDispatchMessage> Snapshot()
    {
        return _messages.ToArray();
    }
}
