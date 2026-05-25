using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public interface INotificationSubscriptionRepository
{
    Task AddAsync(NotificationSubscription subscription, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationSubscription>> ListAsync(CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
