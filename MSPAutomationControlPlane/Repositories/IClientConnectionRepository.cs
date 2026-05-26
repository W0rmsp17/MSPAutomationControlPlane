using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public interface IClientConnectionRepository
{
    Task AddAsync(ClientConnection clientConnection, CancellationToken cancellationToken);

    Task UpdateAsync(ClientConnection clientConnection, CancellationToken cancellationToken);

    Task<ClientConnection?> GetAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClientConnection>> ListAsync(CancellationToken cancellationToken);
}
