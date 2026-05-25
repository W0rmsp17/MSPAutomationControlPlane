using System.Collections.Concurrent;
using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class InMemoryNotificationSubscriptionRepository : INotificationSubscriptionRepository
{
    private readonly ConcurrentDictionary<string, NotificationSubscription> _subscriptions = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(NotificationSubscription subscription, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscription.Id))
        {
            throw new ArgumentException("Subscription id is required.", nameof(subscription));
        }

        _subscriptions[subscription.Id] = subscription;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NotificationSubscription>> ListAsync(CancellationToken cancellationToken)
    {
        var subscriptions = _subscriptions.Values
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<NotificationSubscription>>(subscriptions);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_subscriptions.TryRemove(id, out _));
    }
}
