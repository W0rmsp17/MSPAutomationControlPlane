using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class TableStorageClientConnectionRepository(TableStorageOptions options) : IClientConnectionRepository
{
    private const string PartitionKey = "CLIENT";
    private readonly TableJsonStore<ClientConnection> _store = new(options, "ClientConnections");

    public Task AddAsync(ClientConnection clientConnection, CancellationToken cancellationToken)
    {
        return _store.UpsertAsync(PartitionKey, clientConnection.Id, clientConnection, cancellationToken);
    }

    public Task<ClientConnection?> GetAsync(string id, CancellationToken cancellationToken)
    {
        return _store.GetAsync(PartitionKey, id, cancellationToken);
    }

    public async Task<IReadOnlyList<ClientConnection>> ListAsync(CancellationToken cancellationToken)
    {
        var clientConnections = await _store.ListPartitionAsync(PartitionKey, cancellationToken);
        return clientConnections
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
