using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class ClientConnectionService(
    IClientConnectionRepository clientConnectionRepository,
    AuditService auditService,
    IOperatorContext operatorContext)
{
    public async Task<Result<ClientConnection>> RegisterAsync(
        ClientConnection request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Result<ClientConnection>.Failure(validationError);
        }

        var existing = await clientConnectionRepository.GetAsync(request.Id, cancellationToken);
        if (existing is not null)
        {
            return Result<ClientConnection>.Failure($"Client connection '{request.Id}' is already registered.");
        }

        var clientConnection = request with
        {
            CreatedBy = operatorContext.CurrentOperator,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await clientConnectionRepository.AddAsync(clientConnection, cancellationToken);
        await auditService.WriteAsync(
            AuditEventType.ClientConnectionRegistered,
            operatorContext.CurrentOperator,
            $"Client connection '{clientConnection.Id}' was registered.",
            cancellationToken,
            clientConnectionId: clientConnection.Id,
            resourceId: clientConnection.Id);

        return Result<ClientConnection>.Success(clientConnection);
    }

    public Task<ClientConnection?> GetAsync(string id, CancellationToken cancellationToken)
    {
        return clientConnectionRepository.GetAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<ClientConnection>> ListAsync(CancellationToken cancellationToken)
    {
        return clientConnectionRepository.ListAsync(cancellationToken);
    }

    private static string? Validate(ClientConnection clientConnection)
    {
        if (string.IsNullOrWhiteSpace(clientConnection.Id))
        {
            return "Client connection id is required.";
        }

        if (string.IsNullOrWhiteSpace(clientConnection.DisplayName))
        {
            return "Client display name is required.";
        }

        if (string.IsNullOrWhiteSpace(clientConnection.TenantId))
        {
            return "Client tenant id is required.";
        }

        if (clientConnection.AllowedScopes.Count == 0)
        {
            return "At least one allowed target scope is required.";
        }

        return null;
    }
}
