using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class NotificationSubscriptionService(
    INotificationSubscriptionRepository notificationSubscriptionRepository,
    AuditService auditService,
    IOperatorContext operatorContext)
{
    public async Task<Result<NotificationSubscription>> RegisterAsync(
        NotificationSubscription request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Result<NotificationSubscription>.Failure(validationError);
        }

        var now = DateTimeOffset.UtcNow;
        var subscription = request with
        {
            Id = string.IsNullOrWhiteSpace(request.Id)
                ? $"notif-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"
                : request.Id,
            CreatedBy = operatorContext.CurrentOperator,
            CreatedAt = now
        };

        await notificationSubscriptionRepository.AddAsync(subscription, cancellationToken);
        await auditService.WriteAsync(
            AuditEventType.NotificationSubscriptionRegistered,
            operatorContext.CurrentOperator,
            $"Notification subscription '{subscription.Name}' was registered.",
            cancellationToken,
            resourceId: subscription.Id);

        return Result<NotificationSubscription>.Success(subscription);
    }

    public Task<IReadOnlyList<NotificationSubscription>> ListAsync(CancellationToken cancellationToken)
    {
        return notificationSubscriptionRepository.ListAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var removed = await notificationSubscriptionRepository.DeleteAsync(id, cancellationToken);
        if (removed)
        {
            await auditService.WriteAsync(
                AuditEventType.NotificationSubscriptionDeleted,
                operatorContext.CurrentOperator,
                $"Notification subscription '{id}' was deleted.",
                cancellationToken,
                resourceId: id);
        }

        return removed;
    }

    private static string? Validate(NotificationSubscription subscription)
    {
        if (string.IsNullOrWhiteSpace(subscription.Name))
        {
            return "Notification subscription name is required.";
        }

        if (subscription.Url.Scheme != Uri.UriSchemeHttps)
        {
            return "Notification subscription URL must use HTTPS.";
        }

        if (subscription.EventTypes.Count == 0)
        {
            return "At least one notification event type is required.";
        }

        return null;
    }
}
