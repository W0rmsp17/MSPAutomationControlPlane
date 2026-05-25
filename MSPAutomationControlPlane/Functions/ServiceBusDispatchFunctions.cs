using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Services;

namespace MSPAutomationControlPlane.Functions;

public sealed class ServiceBusDispatchFunctions(JobDispatcher jobDispatcher)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Function("DispatchServiceBusJob")]
    public async Task DispatchServiceBusJob(
        [ServiceBusTrigger("%ServiceBusJobQueueName%", Connection = "ServiceBusConnection")]
        string messageBody,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<JobDispatchMessage>(messageBody, JsonOptions);
        if (message is null)
        {
            throw new InvalidOperationException("Service Bus message could not be deserialized as a job dispatch message.");
        }

        var result = await jobDispatcher.DispatchAsync(message, "servicebus-dispatcher", cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Error);
        }
    }
}
