using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Http;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Functions;

public sealed class NotificationSubscriptionFunctions(NotificationSubscriptionService notificationSubscriptionService)
{
    [Function("RegisterNotificationSubscription")]
    public async Task<HttpResponseData> RegisterNotificationSubscription(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "notification-subscriptions")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var subscription = await request.ReadJsonAsync<NotificationSubscription>(cancellationToken);
        if (subscription is null)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, "Request body is required.");
        }

        var result = await notificationSubscriptionService.RegisterAsync(subscription, cancellationToken);
        if (!result.Succeeded)
        {
            return await request.WriteProblemAsync(HttpStatusCode.BadRequest, result.Error);
        }

        return await request.WriteJsonAsync(HttpStatusCode.Created, result.Value!);
    }

    [Function("ListNotificationSubscriptions")]
    public async Task<HttpResponseData> ListNotificationSubscriptions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notification-subscriptions")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var subscriptions = await notificationSubscriptionService.ListAsync(cancellationToken);
        return await request.WriteJsonAsync(HttpStatusCode.OK, subscriptions);
    }

    [Function("DeleteNotificationSubscription")]
    public async Task<HttpResponseData> DeleteNotificationSubscription(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "notification-subscriptions/{id}")] HttpRequestData request,
        string id,
        CancellationToken cancellationToken)
    {
        var removed = await notificationSubscriptionService.DeleteAsync(id, cancellationToken);
        if (!removed)
        {
            return await request.WriteProblemAsync(HttpStatusCode.NotFound, $"Notification subscription '{id}' was not found.");
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
