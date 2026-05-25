using System.Collections.Concurrent;
using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class InMemoryClientConnectionRepository : IClientConnectionRepository
{
    private readonly ConcurrentDictionary<string, ClientConnection> _clientConnections = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(ClientConnection clientConnection, CancellationToken cancellationToken)
    {
        _clientConnections[clientConnection.Id] = clientConnection;
        return Task.CompletedTask;
    }

    public Task<ClientConnection?> GetAsync(string id, CancellationToken cancellationToken)
    {
        _clientConnections.TryGetValue(id, out var clientConnection);
        return Task.FromResult(clientConnection);
    }

    public Task<IReadOnlyList<ClientConnection>> ListAsync(CancellationToken cancellationToken)
    {
        var connections = _clientConnections.Values
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ClientConnection>>(connections);
    }
}
