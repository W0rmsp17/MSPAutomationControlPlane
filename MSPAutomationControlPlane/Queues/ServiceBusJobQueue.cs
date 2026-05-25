using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Queues;

public sealed class ServiceBusJobQueue : IJobQueue, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public ServiceBusJobQueue(ServiceBusQueueOptions options)
    {
        _client = new ServiceBusClient(options.ConnectionString);
        _sender = _client.CreateSender(options.JobQueueName);
    }

    public async Task EnqueueAsync(JobDispatchMessage message, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(message, JsonOptions);
        var serviceBusMessage = new ServiceBusMessage(body)
        {
            MessageId = message.JobId,
            Subject = "JobDispatch",
            ContentType = "application/json",
            CorrelationId = message.ClientConnectionId
        };

        serviceBusMessage.ApplicationProperties["jobId"] = message.JobId;
        serviceBusMessage.ApplicationProperties["moduleId"] = message.ModuleId;
        serviceBusMessage.ApplicationProperties["moduleVersion"] = message.ModuleVersion;
        serviceBusMessage.ApplicationProperties["clientConnectionId"] = message.ClientConnectionId;

        await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
