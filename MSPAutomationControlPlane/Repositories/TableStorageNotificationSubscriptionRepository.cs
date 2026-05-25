using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class TableStorageNotificationSubscriptionRepository(TableStorageOptions options) : INotificationSubscriptionRepository
{
    private const string PartitionKey = "NOTIFICATION";
    private readonly TableJsonStore<NotificationSubscription> _store = new(options, "NotificationSubscriptions");

    public Task AddAsync(NotificationSubscription subscription, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscription.Id))
        {
            throw new ArgumentException("Subscription id is required.", nameof(subscription));
        }

        return _store.UpsertAsync(PartitionKey, subscription.Id, subscription, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationSubscription>> ListAsync(CancellationToken cancellationToken)
    {
        var subscriptions = await _store.ListPartitionAsync(PartitionKey, cancellationToken);
        return subscriptions
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        return _store.DeleteAsync(PartitionKey, id, cancellationToken);
    }
}
