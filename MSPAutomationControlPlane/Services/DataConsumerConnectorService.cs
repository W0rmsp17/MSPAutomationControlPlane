using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class DataConsumerConnectorService(
    IDataConsumerConnectorRepository connectorRepository,
    AuditService auditService,
    IOperatorContext operatorContext)
{
    public async Task<Result<DataConsumerConnector>> RegisterAsync(
        DataConsumerConnector request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Result<DataConsumerConnector>.Failure(validationError);
        }

        var now = DateTimeOffset.UtcNow;
        var connector = request with
        {
            Id = string.IsNullOrWhiteSpace(request.Id)
                ? $"consumer-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"
                : request.Id,
            CreatedBy = operatorContext.CurrentOperator,
            CreatedAt = now
        };

        await connectorRepository.AddAsync(connector, cancellationToken);
        await auditService.WriteAsync(
            AuditEventType.DataConsumerConnectorRegistered,
            operatorContext.CurrentOperator,
            $"Data consumer connector '{connector.DisplayName}' was registered.",
            cancellationToken,
            resourceId: connector.Id);

        return Result<DataConsumerConnector>.Success(connector);
    }

    public Task<IReadOnlyList<DataConsumerConnector>> ListAsync(CancellationToken cancellationToken)
    {
        return connectorRepository.ListAsync(cancellationToken);
    }

    private static string? Validate(DataConsumerConnector connector)
    {
        if (string.IsNullOrWhiteSpace(connector.DisplayName))
        {
            return "Data consumer connector display name is required.";
        }

        if (connector.Type == DataConsumerConnectorType.AI && string.IsNullOrWhiteSpace(connector.PromptTemplateId))
        {
            return "AI connectors must declare a prompt template ID.";
        }

        if (connector.Policy.MaxInputBytes <= 0)
        {
            return "Data consumer connector max input bytes must be greater than zero.";
        }

        return null;
    }
}
